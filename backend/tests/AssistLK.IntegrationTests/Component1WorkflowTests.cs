using AssistLK.Agents.Agents;
using AssistLK.Agents.Core;
using AssistLK.Agents.Models;
using AssistLK.Agents.Tools;
using AssistLK.Application.Interfaces;
using AssistLK.Application.Services;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.IntegrationTests;

public class Component1WorkflowTests
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
        public ToolRegistry ToolReg { get; }
        public ToolExecutor ToolExec { get; }
        public ProblemUnderstandingAgent Agent { get; }
        public ServiceRequestService RequestService { get; }
        public List<ServiceRequest> Requests { get; } = new();
        public List<ProblemAnalysis> Analyses { get; } = new();
        public ProblemUnderstandingWorkflowService Workflow { get; }

        public WorkflowTestFixture()
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

            ToolReg = new ToolRegistry();
            ToolReg.Register(new ProblemClassificationTool());
            ToolReg.Register(new LocationExtractionTool());
            ToolReg.Register(new ServiceKnowledgeTool());

            ToolExec = new ToolExecutor(ToolReg);
            Agent = new ProblemUnderstandingAgent(ToolExec);

            Registry = new AgentRegistry();
            Registry.Register(Agent);

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

    [Fact]
    public async Task Workflow_ClearInput_CompletesAndTransitionsToAnalyzed()
    {
        var fixture = new WorkflowTestFixture();
        var requestId = Guid.NewGuid();

        fixture.Requests.Add(new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Water pipe has burst and is flooding the entire kitchen floor",
            LocationText = "Kandy, Sri Lanka",
            Latitude = 7.2906m,
            Longitude = 80.6337m,
            Status = ServiceRequestStatus.Created
        });

        var input = new ProblemUnderstandingInput
        {
            ServiceRequestId = requestId,
            Description = "Water pipe has burst and is flooding the entire kitchen floor",
            LocationText = "Kandy, Sri Lanka",
            Latitude = 7.2906m,
            Longitude = 80.6337m
        };

        var result = await fixture.Workflow.AnalyzeAsync(input);

        Assert.True(result.Success);
        Assert.Equal("Analyzed", result.Outcome);
        Assert.NotNull(result.Output);
        Assert.Equal("Plumbing", result.Output.Category);
        Assert.Equal(ServiceRequestUrgency.High, result.Output.Urgency);
        Assert.False(result.Output.NeedsMoreInformation);

        // Verify Domain entity updated
        var request = fixture.Requests.Single();
        Assert.Equal(ServiceRequestStatus.Analyzed, request.Status);
        Assert.Equal("Plumbing", request.Category);
        Assert.Equal(ServiceRequestUrgency.High, request.Urgency);

        // Verify ProblemAnalysis entity persisted
        var analysis = fixture.Analyses.Single();
        Assert.Equal(requestId, analysis.ServiceRequestId);
        Assert.Equal("ProblemUnderstandingAgent", analysis.AgentName);
        Assert.True(analysis.Confidence >= 0.7m);

        // Verify Tool calls happened through execution metric
        var execMetric = await fixture.Db.AgentExecutionMetrics.SingleAsync();
        Assert.Equal(3, execMetric.ToolCalls);

        // Verify Memory stored
        var memoryItems = await fixture.MemoryService.GetAllAsync(result.WorkflowId);
        Assert.Equal(8, memoryItems.Count);

        // Verify Workflow status
        var wf = await fixture.WorkflowService.GetAsync(result.WorkflowId);
        Assert.NotNull(wf);
        Assert.Equal("Completed", wf.Status);
    }

    [Fact]
    public async Task Workflow_AmbiguousInput_CompletesWithAwaitingInformation()
    {
        var fixture = new WorkflowTestFixture();
        var requestId = Guid.NewGuid();

        fixture.Requests.Add(new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Something is broken in my house",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Created
        });

        var input = new ProblemUnderstandingInput
        {
            ServiceRequestId = requestId,
            Description = "Something is broken in my house",
            LocationText = "Colombo"
        };

        var result = await fixture.Workflow.AnalyzeAsync(input);

        Assert.True(result.Success);
        Assert.Equal("AwaitingInformation", result.Outcome);
        Assert.NotNull(result.Output);
        Assert.True(result.Output.NeedsMoreInformation);
        Assert.NotEmpty(result.Output.FollowUpQuestions);

        // Verify Domain entity status is AwaitingInformation
        var request = fixture.Requests.Single();
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, request.Status);
    }

    [Fact]
    public void Agent_DoesNotInjectDatabaseOrRepositoryDependencies()
    {
        var ctors = typeof(ProblemUnderstandingAgent).GetConstructors();
        Assert.Single(ctors);
        var pTypes = ctors[0].GetParameters().Select(p => p.ParameterType.Name).ToArray();
        Assert.Contains("ToolExecutor", pTypes);
        Assert.DoesNotContain(pTypes, name => name.Contains("DbContext"));
        Assert.DoesNotContain(pTypes, name => name.Contains("Repository"));
    }

    [Fact]
    public async Task Workflow_InvokesToolExecutorAndSetsToolCallCount()
    {
        var fixture = new WorkflowTestFixture();
        var requestId = Guid.NewGuid();

        fixture.Requests.Add(new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "My car battery died and the engine won't start",
            LocationText = "Galle",
            Status = ServiceRequestStatus.Created
        });

        var input = new ProblemUnderstandingInput
        {
            ServiceRequestId = requestId,
            Description = "My car battery died and the engine won't start",
            LocationText = "Galle"
        };

        var result = await fixture.Workflow.AnalyzeAsync(input);

        Assert.True(result.Success);

        // Check execution metric recorded in DB
        var metrics = await fixture.Db.AgentExecutionMetrics.ToListAsync();
        Assert.Single(metrics);
        Assert.Equal(3, metrics.Single().ToolCalls);
        Assert.Equal("Completed", metrics.Single().Status);
    }

    [Fact]
    public async Task Workflow_RecordsExecutionAndAuditLog()
    {
        var fixture = new WorkflowTestFixture();
        var requestId = Guid.NewGuid();

        fixture.Requests.Add(new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Electric switch board is smoking",
            LocationText = "Colombo 07",
            Status = ServiceRequestStatus.Created
        });

        var input = new ProblemUnderstandingInput
        {
            ServiceRequestId = requestId,
            Description = "Electric switch board is smoking",
            LocationText = "Colombo 07"
        };

        var result = await fixture.Workflow.AnalyzeAsync(input);

        var executions = await fixture.Db.AgentExecutions.ToListAsync();
        Assert.Single(executions);
        Assert.Equal("Completed", executions.Single().Status);

        var actions = await fixture.Db.AgentActions.ToListAsync();
        Assert.Single(actions);
        Assert.Equal("ANALYZE_PROBLEM", actions.Single().ActionType);
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
