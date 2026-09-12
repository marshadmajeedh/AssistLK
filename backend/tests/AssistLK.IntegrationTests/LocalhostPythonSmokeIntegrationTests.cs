using AssistLK.Agents.Adapters;
using AssistLK.Agents.Clients;
using AssistLK.Agents.Configuration;
using AssistLK.Agents.Core;
using AssistLK.Application.Interfaces;
using AssistLK.Application.Services;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssistLK.IntegrationTests;

/// <summary>
/// Section 16 Integration Verification: Real communication between ASP.NET Core
/// ProblemUnderstandingWorkflowService and the live Python service running on localhost:8001.
/// </summary>
public class LocalhostPythonSmokeIntegrationTests
{
    private static async Task<bool> IsPythonServiceRunningAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var res = await client.GetAsync("http://127.0.0.1:8001/health");
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RealPythonIntegration_AnalyzeAsync_ExecutesLangGraphAndPersistsAnalysis()
    {
        var optIn = Environment.GetEnvironmentVariable("RUN_PYTHON_AGENT_SMOKE_TESTS");
        if (!string.Equals(optIn, "true", StringComparison.OrdinalIgnoreCase))
        {
            // Explicit opt-in required (RUN_PYTHON_AGENT_SMOKE_TESTS=true)
            // Smoke test is skipped during ordinary test runs
            return;
        }

        // Check if Python service is reachable on localhost:8001
        var isRunning = await IsPythonServiceRunningAsync();
        if (!isRunning)
        {
            // Skip when running in an environment without a running Python daemon
            return;
        }

        // Setup real in-memory DB and real workflow service connected to http://127.0.0.1:8001
        var dbOptions = new DbContextOptionsBuilder<AssistLKDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new AssistLKDbContext(dbOptions);

        var workflowService = new AgentWorkflowService(db);
        var memoryService = new AgentMemoryService(db);
        var contextService = new AgentContextService(memoryService);
        var monitoringService = new AgentMonitoringService(db);
        var safetyService = new AgentSafetyService(new AgentSafetyPolicyEngine(), db);

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://127.0.0.1:8001"),
            Timeout = TimeSpan.FromSeconds(45)
        };

        var options = new AgentServicesOptions
        {
            ProblemUnderstandingMode = "ExternalPython",
            ProblemUnderstandingUrl = "http://127.0.0.1:8001"
        };

        var pythonClient = new ProblemUnderstandingHttpClient(
            httpClient,
            options,
            NullLogger<ProblemUnderstandingHttpClient>.Instance);

        var adapter = new ExternalProblemUnderstandingAgentAdapter(
            pythonClient,
            NullLogger<ExternalProblemUnderstandingAgentAdapter>.Instance);

        var registry = new AgentRegistry();
        registry.Register(adapter);

        var orchestrator = new AgentOrchestrator(registry);

        var requests = new List<ServiceRequest>();
        var analyses = new List<ProblemAnalysis>();
        var reqRepo = new SmokeServiceRequestRepository(requests, analyses);
        var anaRepo = new SmokeProblemAnalysisRepository(analyses);
        var requestService = new ServiceRequestService(reqRepo, anaRepo);

        var workflow = new ProblemUnderstandingWorkflowService(
            workflowService,
            contextService,
            memoryService,
            monitoringService,
            safetyService,
            orchestrator,
            registry,
            requestService);

        var requestId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        requests.Add(new ServiceRequest
        {
            Id = requestId,
            CustomerId = customerId,
            Description = "Water pipe burst in the kitchen causing serious flooding on the floor.",
            LocationText = "Colombo 07, Cinnamon Gardens",
            Status = ServiceRequestStatus.Created
        });

        // Execute workflow against real live Python service
        var result = await workflow.AnalyzeAsync(requestId, customerId);

        // Assertions verifying Section 16:
        // ASP.NET -> Python -> valid ProblemUnderstandingOutput -> ProblemAnalysis persisted -> ServiceRequest lifecycle updated
        Assert.True(result.Success);
        Assert.Equal("Analyzed", result.Outcome);
        Assert.NotNull(result.Output);
        Assert.Equal("Plumbing", result.Category);
        Assert.False(result.Output.NeedsMoreInformation);
        Assert.True(result.Output.Confidence >= 0.70m);

        // Verify Domain entity updated
        var request = requests.Single();
        Assert.Equal(ServiceRequestStatus.Analyzed, request.Status);
        Assert.Equal("Plumbing", request.Category);

        // Verify ProblemAnalysis entity persisted
        var analysis = analyses.Single();
        Assert.Equal(requestId, analysis.ServiceRequestId);
        Assert.Equal("ProblemUnderstandingAgent", analysis.AgentName);
        Assert.True(analysis.Confidence >= 0.70m);

        // Verify Execution metrics stored
        var metric = await db.AgentExecutionMetrics.FirstOrDefaultAsync();
        Assert.NotNull(metric);
        Assert.Equal("Completed", metric.Status);
    }

    private sealed class SmokeServiceRequestRepository : IServiceRequestRepository
    {
        private readonly List<ServiceRequest> _requests;
        private readonly List<ProblemAnalysis> _analyses;

        public SmokeServiceRequestRepository(List<ServiceRequest> requests, List<ProblemAnalysis> analyses)
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

    private sealed class SmokeProblemAnalysisRepository : IProblemAnalysisRepository
    {
        private readonly List<ProblemAnalysis> _analyses;

        public SmokeProblemAnalysisRepository(List<ProblemAnalysis> analyses)
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
