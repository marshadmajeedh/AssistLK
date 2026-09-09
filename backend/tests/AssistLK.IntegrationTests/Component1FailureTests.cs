using AssistLK.Agents.Agents;
using AssistLK.Agents.Core;
using AssistLK.Agents.Models;
using AssistLK.Agents.Services;
using AssistLK.Agents.Tools;
using AssistLK.Application.Interfaces;
using AssistLK.Application.Services;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssistLK.IntegrationTests;

public class Component1FailureTests
{
    [Fact]
    public async Task ToolExecutor_UnknownTool_ReturnsToolNotFoundResult()
    {
        var registry = new ToolRegistry();
        var executor = new ToolExecutor(registry);

        var result = await executor.ExecuteAsync("NonExistentTool", new Dictionary<string, object>());

        Assert.False(result.Success);
        Assert.Equal("TOOL_NOT_FOUND", result.ErrorCode);
        Assert.Contains("not found", result.Message);
    }

    [Fact]
    public async Task Workflow_RejectsInvalidConfidenceScoreWithoutPersistingAnalysis()
    {
        var options = new DbContextOptionsBuilder<AssistLKDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new AssistLKDbContext(options);
        var workflowService = new AgentWorkflowService(db);
        var memoryService = new AgentMemoryService(db);
        var contextService = new AgentContextService(memoryService);
        var monitoringService = new AgentMonitoringService(db);
        var safetyService = new AgentSafetyService(new AgentSafetyPolicyEngine(), db);

        var requests = new List<ServiceRequest>();
        var analyses = new List<ProblemAnalysis>();
        var reqRepo = new InMemoryServiceRequestRepository(requests, analyses);
        var anaRepo = new InMemoryProblemAnalysisRepository(analyses);
        var requestService = new ServiceRequestService(reqRepo, anaRepo);

        // A faulty agent returning invalid confidence > 1.0
        var faultyAgent = new FaultyAgent(new ProblemUnderstandingOutput
        {
            Category = "Plumbing",
            ProblemSummary = "Valid summary",
            Confidence = 1.5m, // Invalid!
            Urgency = ServiceRequestUrgency.Medium
        });

        var registry = new AgentRegistry();
        registry.Register(faultyAgent);
        var orchestrator = new AgentOrchestrator(registry);

        var workflow = new ProblemUnderstandingWorkflowService(
            workflowService,
            contextService,
            memoryService,
            monitoringService,
            safetyService,
            orchestrator,
            registry,
            requestService);

        var serviceRequestId = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = serviceRequestId,
            CustomerId = Guid.NewGuid(),
            Description = "Leaking pipe",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Created
        });

        var result = await workflow.AnalyzeAsync(new ProblemUnderstandingInput
        {
            ServiceRequestId = serviceRequestId,
            Description = "Leaking pipe"
        });

        Assert.False(result.Success);
        Assert.Equal("Failed", result.Outcome);
        Assert.Contains("confidence", result.ErrorMessage?.ToLowerInvariant() ?? "");

        // Verify no ProblemAnalysis was persisted
        Assert.Empty(analyses);

        // Verify workflow status is Failed
        var wf = await workflowService.GetAsync(result.WorkflowId);
        Assert.NotNull(wf);
        Assert.Equal("Failed", wf.Status);

        // Verify execution metric was recorded as Failed
        var metric = await db.AgentExecutionMetrics.FirstOrDefaultAsync(x => x.WorkflowId == result.WorkflowId);
        Assert.NotNull(metric);
        Assert.Equal("Failed", metric.Status);
    }

    [Fact]
    public async Task Workflow_RejectsEmptySummaryWithoutPersistingAnalysis()
    {
        var options = new DbContextOptionsBuilder<AssistLKDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new AssistLKDbContext(options);
        var workflowService = new AgentWorkflowService(db);
        var memoryService = new AgentMemoryService(db);
        var contextService = new AgentContextService(memoryService);
        var monitoringService = new AgentMonitoringService(db);
        var safetyService = new AgentSafetyService(new AgentSafetyPolicyEngine(), db);

        var requests = new List<ServiceRequest>();
        var analyses = new List<ProblemAnalysis>();
        var reqRepo = new InMemoryServiceRequestRepository(requests, analyses);
        var anaRepo = new InMemoryProblemAnalysisRepository(analyses);
        var requestService = new ServiceRequestService(reqRepo, anaRepo);

        var faultyAgent = new FaultyAgent(new ProblemUnderstandingOutput
        {
            Category = "Plumbing",
            ProblemSummary = "   ", // Empty!
            Confidence = 0.8m,
            Urgency = ServiceRequestUrgency.Medium
        });

        var registry = new AgentRegistry();
        registry.Register(faultyAgent);
        var orchestrator = new AgentOrchestrator(registry);

        var workflow = new ProblemUnderstandingWorkflowService(
            workflowService,
            contextService,
            memoryService,
            monitoringService,
            safetyService,
            orchestrator,
            registry,
            requestService);

        var serviceRequestId = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = serviceRequestId,
            CustomerId = Guid.NewGuid(),
            Description = "Leaking pipe",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Created
        });

        var result = await workflow.AnalyzeAsync(new ProblemUnderstandingInput
        {
            ServiceRequestId = serviceRequestId,
            Description = "Leaking pipe"
        });

        Assert.False(result.Success);
        Assert.Equal("Failed", result.Outcome);
        Assert.Empty(analyses);
    }

    [Fact]
    public async Task Workflow_FailsGracefullyWhenServiceRequestNotFound()
    {
        var options = new DbContextOptionsBuilder<AssistLKDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new AssistLKDbContext(options);
        var workflowService = new AgentWorkflowService(db);
        var memoryService = new AgentMemoryService(db);
        var contextService = new AgentContextService(memoryService);
        var monitoringService = new AgentMonitoringService(db);
        var safetyService = new AgentSafetyService(new AgentSafetyPolicyEngine(), db);

        var requests = new List<ServiceRequest>();
        var analyses = new List<ProblemAnalysis>();
        var reqRepo = new InMemoryServiceRequestRepository(requests, analyses);
        var anaRepo = new InMemoryProblemAnalysisRepository(analyses);
        var requestService = new ServiceRequestService(reqRepo, anaRepo);

        var toolRegistry = new ToolRegistry();
        var toolExecutor = new ToolExecutor(toolRegistry);
        var registry = new AgentRegistry();
        registry.Register(new ProblemUnderstandingAgent(toolExecutor, new GeminiService(), NullLogger<ProblemUnderstandingAgent>.Instance));
        var orchestrator = new AgentOrchestrator(registry);

        var workflow = new ProblemUnderstandingWorkflowService(
            workflowService,
            contextService,
            memoryService,
            monitoringService,
            safetyService,
            orchestrator,
            registry,
            requestService);

        // Non-existent ID
        var nonExistentId = Guid.NewGuid();

        var result = await workflow.AnalyzeAsync(new ProblemUnderstandingInput
        {
            ServiceRequestId = nonExistentId,
            Description = "Leaking pipe"
        });

        Assert.False(result.Success);
        Assert.Equal("Failed", result.Outcome);
        Assert.Contains("not found", result.ErrorMessage?.ToLowerInvariant() ?? "");
    }

    [Fact]
    public async Task Agent_ContinuesWithGeminiWhenClassificationToolMissing()
    {
        // Fixed pipeline: classification tool is NOT registered (returns TOOL_NOT_FOUND),
        // but Gemini is now the primary engine and runs regardless of tool outcome.
        var toolRegistry = new ToolRegistry();
        // Register location and service knowledge, but intentionally omit ProblemClassificationTool
        toolRegistry.Register(new LocationExtractionTool());
        toolRegistry.Register(new ServiceKnowledgeTool());

        var toolExecutor = new ToolExecutor(toolRegistry);
        // GeminiService uses offline simulation (no API key in test env)
        var agent = new ProblemUnderstandingAgent(
            toolExecutor,
            new GeminiService(),
            NullLogger<ProblemUnderstandingAgent>.Instance);

        var context = new AgentContext
        {
            WorkflowId = Guid.NewGuid(),
            Input = "Water pipe has burst and is flooding the house",
            Data =
            {
                [nameof(ProblemUnderstandingInput)] = new ProblemUnderstandingInput
                {
                    ServiceRequestId = Guid.NewGuid(),
                    Description = "Water pipe has burst and is flooding the house",
                    LocationText = "Colombo"
                }
            }
        };

        var result = await agent.ExecuteAsync(context);

        Assert.True(result.Success);
        var output = Assert.IsType<ProblemUnderstandingOutput>(result.Data);

        // Gemini runs as primary engine: offline simulation identifies burst pipe as Plumbing.
        // The classification tool being missing is non-fatal — Gemini output is authoritative.
        Assert.Equal("Plumbing", output.Category);
        Assert.Equal("Analysed", result.NextAction);
        Assert.False(output.NeedsMoreInformation);
    }

    private sealed class FaultyAgent : AssistLK.Agents.Abstractions.IAgent
    {
        private readonly ProblemUnderstandingOutput _output;
        public FaultyAgent(ProblemUnderstandingOutput output) => _output = output;
        public string Name => "ProblemUnderstandingAgent";
        public Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(new AgentResult
            {
                Success = true,
                Message = "Faulty result",
                Data = _output
            });
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
