using System.Net;
using System.Text;
using System.Text.Json;
using AssistLK.Agents.Adapters;
using AssistLK.Agents.Clients;
using AssistLK.Agents.Configuration;
using AssistLK.Agents.Core;
using AssistLK.Agents.DTOs;
using AssistLK.Agents.Models;
using AssistLK.Application.Interfaces;
using AssistLK.Application.Services;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssistLK.IntegrationTests;

/// <summary>
/// End-to-end workflow tests for ProblemUnderstandingWorkflowService operating with
/// the ExternalProblemUnderstandingAgentAdapter.
/// Uses mocked HTTP infrastructure to guarantee deterministic execution without requiring
/// a running Python process during standard test runs.
/// </summary>
public class ExternalProblemUnderstandingWorkflowIntegrationTests
{
    private sealed class WorkflowTestFixture
    {
        public AssistLKDbContext Db { get; }
        public AgentWorkflowService WorkflowService { get; }
        public AgentContextService ContextService { get; }
        public AgentMemoryService MemoryService { get; }
        public AgentMonitoringService MonitoringService { get; }
        public AgentSafetyService SafetyService { get; }
        public AgentOrchestrator Orchestrator { get; }
        public AgentRegistry Registry { get; }
        public ExternalProblemUnderstandingAgentAdapter Adapter { get; }
        public ServiceRequestService RequestService { get; }
        public List<ServiceRequest> Requests { get; } = new();
        public List<ProblemAnalysis> Analyses { get; } = new();
        public ProblemUnderstandingWorkflowService Workflow { get; }

        public WorkflowTestFixture(Func<HttpRequestMessage, HttpResponseMessage> httpHandler)
        {
            var options = new DbContextOptionsBuilder<AssistLKDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            Db = new AssistLKDbContext(options);

            WorkflowService = new AgentWorkflowService(Db);
            MemoryService = new AgentMemoryService(Db);
            ContextService = new AgentContextService(MemoryService);
            MonitoringService = new AgentMonitoringService(Db);
            SafetyService = new AgentSafetyService(new AgentSafetyPolicyEngine(), Db);

            var mockHandler = new TestHttpMessageHandler(httpHandler);
            var httpClient = new HttpClient(mockHandler)
            {
                BaseAddress = new Uri("http://127.0.0.1:8001")
            };

            var client = new ProblemUnderstandingHttpClient(
                httpClient,
                new AgentServicesOptions { ProblemUnderstandingUrl = "http://127.0.0.1:8001" },
                NullLogger<ProblemUnderstandingHttpClient>.Instance);

            Adapter = new ExternalProblemUnderstandingAgentAdapter(
                client,
                NullLogger<ExternalProblemUnderstandingAgentAdapter>.Instance);

            Registry = new AgentRegistry();
            Registry.Register(Adapter);

            Orchestrator = new AgentOrchestrator(Registry);

            var reqRepo = new InMemoryServiceRequestRepository(Requests, Analyses);
            var anaRepo = new InMemoryProblemAnalysisRepository(Analyses);
            RequestService = new ServiceRequestService(reqRepo, anaRepo);

            Workflow = new ProblemUnderstandingWorkflowService(
                WorkflowService,
                ContextService,
                MemoryService,
                MonitoringService,
                SafetyService,
                Orchestrator,
                Registry,
                RequestService);
        }
    }

    private class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    private static HttpResponseMessage JsonResponse(object obj, HttpStatusCode code = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(code)
        {
            Content = new StringContent(JsonSerializer.Serialize(obj), Encoding.UTF8, "application/json")
        };
    }

    [Fact]
    public async Task ExternalAdapterWorkflow_ConfidentPlumbing_CompletesAndTransitionsToAnalyzed()
    {
        var fixture = new WorkflowTestFixture(_ =>
            JsonResponse(new AgentExecutionResponseDto
            {
                RequestId = Guid.NewGuid(),
                Success = true,
                Result = new ProblemUnderstandingOutputPayloadDto
                {
                    Category = "Plumbing",
                    ProblemSummary = "Water pipe burst causing kitchen flooding.",
                    Urgency = "High",
                    NeedsMoreInformation = false,
                    Confidence = 0.94m,
                    ExtractedLocation = "Colombo 07",
                    AdditionalInformation = new Dictionary<string, string>
                    {
                        ["WaterMainClosed"] = "True"
                    }
                },
                Metadata = new ExecutionMetadataPayloadDto
                {
                    AgentName = "ProblemUnderstandingAgent",
                    Provider = "offline",
                    Degraded = false,
                    DurationMs = 15,
                    ToolExecutions = new List<ToolExecutionAuditPayloadDto>
                    {
                        new() { Tool = "LocationExtractionTool", Success = true, DurationMs = 2 },
                        new() { Tool = "ProblemClassificationTool", Success = true, DurationMs = 2 }
                    }
                }
            }));

        var requestId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        fixture.Requests.Add(new ServiceRequest
        {
            Id = requestId,
            CustomerId = customerId,
            Description = "Water pipe burst in kitchen, severe flooding",
            LocationText = "Colombo 07",
            Status = ServiceRequestStatus.Created
        });

        var result = await fixture.Workflow.AnalyzeAsync(requestId, customerId);

        // 1. Verify workflow result
        Assert.True(result.Success);
        Assert.Equal("Analyzed", result.Outcome);
        Assert.NotNull(result.Output);
        Assert.Equal("Plumbing", result.Category);
        Assert.Equal(ServiceRequestUrgency.High, result.Urgency);
        Assert.Equal(0.94m, result.Confidence);
        Assert.False(result.NeedsMoreInformation);

        // 2. Verify domain entity status transition
        var request = fixture.Requests.Single();
        Assert.Equal(ServiceRequestStatus.Analyzed, request.Status);
        Assert.Equal("Plumbing", request.Category);
        Assert.Equal(ServiceRequestUrgency.High, request.Urgency);

        // 3. Verify ProblemAnalysis entity persisted in database
        var analysis = fixture.Analyses.Single();
        Assert.Equal(requestId, analysis.ServiceRequestId);
        Assert.Equal("ProblemUnderstandingAgent", analysis.AgentName);
        Assert.Equal("Water pipe burst causing kitchen flooding.", analysis.DetectedProblem);
        Assert.Equal(0.94m, analysis.Confidence);

        // 4. Verify tool call count recorded in execution metrics
        var execMetric = await fixture.Db.AgentExecutionMetrics.SingleAsync();
        Assert.Equal(2, execMetric.ToolCalls);

        // 5. Verify workflow state is Completed
        var wf = await fixture.WorkflowService.GetAsync(result.WorkflowId);
        Assert.NotNull(wf);
        Assert.Equal("Completed", wf.Status);
    }

    [Fact]
    public async Task ExternalAdapterWorkflow_DegradedOutput_TransitionsToAwaitingInformation()
    {
        var fixture = new WorkflowTestFixture(_ =>
            JsonResponse(new AgentExecutionResponseDto
            {
                RequestId = Guid.NewGuid(),
                Success = true,
                Result = new ProblemUnderstandingOutputPayloadDto
                {
                    Category = "Unclassified",
                    ProblemSummary = "Ambiguous description. Unable to determine service category.",
                    Urgency = "Unknown",
                    NeedsMoreInformation = true,
                    Confidence = 0.20m,
                    FollowUpQuestions = new List<string>
                    {
                        "What specific appliance or equipment is affected?"
                    }
                },
                Metadata = new ExecutionMetadataPayloadDto
                {
                    AgentName = "ProblemUnderstandingAgent",
                    Provider = "offline",
                    Degraded = true
                }
            }));

        var requestId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        fixture.Requests.Add(new ServiceRequest
        {
            Id = requestId,
            CustomerId = customerId,
            Description = "Something stopped working",
            LocationText = "Kandy",
            Status = ServiceRequestStatus.Created
        });

        var result = await fixture.Workflow.AnalyzeAsync(requestId, customerId);

        Assert.True(result.Success);
        Assert.Equal("AwaitingInformation", result.Outcome);
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, result.Status);

        var request = fixture.Requests.Single();
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, request.Status);
    }

    [Fact]
    public async Task ExternalAdapterWorkflow_Http500Failure_RecoversPreAnalysisStatus()
    {
        var fixture = new WorkflowTestFixture(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Internal Error", Encoding.UTF8, "text/plain")
            });

        var requestId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        fixture.Requests.Add(new ServiceRequest
        {
            Id = requestId,
            CustomerId = customerId,
            Description = "Electric sparks from wall socket",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Created
        });

        // The workflow should fail safely and return a Failed result
        var result = await fixture.Workflow.AnalyzeAsync(requestId, customerId);

        Assert.False(result.Success);
        Assert.Equal("Failed", result.Outcome);

        // State recovery must restore the pre-analysis state (Created)
        var request = fixture.Requests.Single();
        Assert.Equal(ServiceRequestStatus.Created, request.Status);

        // No ProblemAnalysis record must be persisted
        Assert.Empty(fixture.Analyses);
    }

    private sealed class InMemoryServiceRequestRepository : IServiceRequestRepository
    {
        private readonly List<ServiceRequest> _requests;
        private readonly List<ProblemAnalysis> _analyses;

        public InMemoryServiceRequestRepository(List<ServiceRequest> requests, List<ProblemAnalysis> analyses)
        {
            _requests = requests;
            _analyses = analyses;
        }

        public Task<ServiceRequest?> GetByIdAsync(Guid id, bool includeProblemAnalyses = false, CancellationToken cancellationToken = default)
        {
            var req = _requests.SingleOrDefault(x => x.Id == id);
            if (req != null && includeProblemAnalyses)
            {
                req.ProblemAnalyses = _analyses.Where(a => a.ServiceRequestId == id).ToList();
            }
            return Task.FromResult(req);
        }

        public Task<ServiceRequest?> GetByIdAndCustomerIdAsync(Guid id, Guid customerId, bool includeProblemAnalyses = false, CancellationToken cancellationToken = default)
            => Task.FromResult(_requests.SingleOrDefault(x => x.Id == id && x.CustomerId == customerId));

        public Task<IReadOnlyList<ServiceRequest>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ServiceRequest>>(_requests.Where(x => x.CustomerId == customerId).ToArray());

        public Task<IReadOnlyList<ServiceRequest>> GetByStatusAsync(ServiceRequestStatus status, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ServiceRequest>>(_requests.Where(x => x.Status == status).ToArray());

        public Task AddAsync(ServiceRequest serviceRequest, CancellationToken cancellationToken = default)
        {
            _requests.Add(serviceRequest);
            return Task.CompletedTask;
        }

        public void Update(ServiceRequest serviceRequest) { }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryProblemAnalysisRepository : IProblemAnalysisRepository
    {
        private readonly List<ProblemAnalysis> _analyses;

        public InMemoryProblemAnalysisRepository(List<ProblemAnalysis> analyses)
        {
            _analyses = analyses;
        }

        public Task<IReadOnlyList<ProblemAnalysis>> GetByServiceRequestIdAsync(Guid serviceRequestId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProblemAnalysis>>(_analyses.Where(a => a.ServiceRequestId == serviceRequestId).ToArray());

        public Task<ProblemAnalysis?> GetMostRecentByServiceRequestIdAsync(Guid serviceRequestId, CancellationToken cancellationToken = default)
            => Task.FromResult(_analyses.Where(a => a.ServiceRequestId == serviceRequestId).OrderByDescending(a => a.CreatedAt).FirstOrDefault());

        public Task AddAsync(ProblemAnalysis problemAnalysis, CancellationToken cancellationToken = default)
        {
            _analyses.Add(problemAnalysis);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
