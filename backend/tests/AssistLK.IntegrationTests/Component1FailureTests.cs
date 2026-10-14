using AssistLK.Agents.Adapters;
using AssistLK.Agents.Core;
using AssistLK.Agents.Models;
using AssistLK.IntegrationTests.TestDoubles;
using AssistLK.Application.Interfaces;
using AssistLK.Application.ServiceRequests.DTOs;
using AssistLK.Application.Services;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssistLK.IntegrationTests;

/// <summary>
/// What this class tests:
///   Failure handling, error isolation, cancellation tokens, schema validation, and recovery workflows for Component 1 Problem Understanding.
/// Why these tests are needed:
///   Guarantees that when external AI services crash, time out, return invalid scores/summaries, or when clients disconnect,
///   service requests cleanly revert to their pre-analysis state (Created or AwaitingInformation) instead of becoming stuck in 'Analyzing'.
/// What is real and what is mocked/faked:
///   - Real: AgentWorkflowService, AgentMemoryService, AgentContextService, AgentMonitoringService, AgentSafetyService,
///     ProblemUnderstandingWorkflowService, ToolRegistry, and ToolExecutor backed by EF Core In-Memory database.
///   - Mocked/Faked: Agent adapters (FaultyAgent, ThrowingAgent, CancellingAgent, DisconnectingAgent, MidExecutionCancellingAgent)
///     and in-memory repositories (InMemoryServiceRequestRepository, InMemoryProblemAnalysisRepository).
/// Prerequisites:
///   None. Runs completely in-memory without external services or network calls.
/// Run from repository root:
///   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.Component1FailureTests"
/// </summary>
public class Component1FailureTests
{
    /// <summary>
    /// Purpose: Checks that invoking a non-registered tool name returns a tool-not-found error result.
    /// Why: Prevents crashes when an LLM agent hallucinates or calls an unrecognised tool function.
    /// Scenario: ToolExecutor executes "NonExistentTool" against an empty ToolRegistry.
    /// Pass condition: Success is false, ErrorCode is "TOOL_NOT_FOUND", and message contains "not found".
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1FailureTests.ToolExecutor_UnknownTool_ReturnsToolNotFoundResult"
    /// </summary>
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

    /// <summary>
    /// Purpose: Checks that agent output with an out-of-range confidence score (> 1.0) is rejected and not saved.
    /// Why: Protects the database against invalid machine learning confidence scores and rolls back request status.
    /// Scenario: FaultyAgent returns confidence = 1.5m for a newly created request.
    /// Pass condition: Workflow fails, error mentions confidence, no ProblemAnalysis is saved, and status resets to Created.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1FailureTests.Workflow_RejectsInvalidConfidenceScoreWithoutPersistingAnalysis"
    /// </summary>
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
            requestService,
            AssistLK.IntegrationTests.TestDoubles.TestAnalysisEvidence.Create(reqRepo));

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

        // Verify ServiceRequest status was recovered to Created
        Assert.Equal(ServiceRequestStatus.Created, requests.Single().Status);
    }

    /// <summary>
    /// Purpose: Checks that agent output with an empty or whitespace summary is rejected and not saved.
    /// Why: Ensures meaningless or blank problem descriptions from LLMs are never committed to the database.
    /// Scenario: FaultyAgent returns whitespace ProblemSummary ("   ").
    /// Pass condition: Workflow fails, no ProblemAnalysis is saved, and request status resets to Created.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1FailureTests.Workflow_RejectsEmptySummaryWithoutPersistingAnalysis"
    /// </summary>
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
            requestService,
            AssistLK.IntegrationTests.TestDoubles.TestAnalysisEvidence.Create(reqRepo));

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

        // Verify ServiceRequest status was recovered to Created
        Assert.Equal(ServiceRequestStatus.Created, requests.Single().Status);
    }

    /// <summary>
    /// Purpose: Checks that running problem analysis for a non-existent service request ID fails gracefully without an unhandled exception.
    /// Why: Prevents unhandled server exceptions when a workflow is triggered for a deleted or invalid request ID.
    /// Scenario: Analysis is started with a random Guid not present in the repository.
    /// Pass condition: Result success is false, outcome is "Failed", and error message contains "not found".
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1FailureTests.Workflow_NonExistentServiceRequestId_FailsGracefully"
    /// </summary>
    [Fact]
    public async Task Workflow_NonExistentServiceRequestId_FailsGracefully()
    {
        var options = new DbContextOptionsBuilder<AssistLKDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new AssistLKDbContext(options);

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

        var registry = new AgentRegistry();
        registry.Register(new ExternalProblemUnderstandingAgentAdapter(
            new FakeProblemUnderstandingClient(),
            NullLogger<ExternalProblemUnderstandingAgentAdapter>.Instance));
        var orchestrator = new AgentOrchestrator(registry);

        var workflow = new ProblemUnderstandingWorkflowService(
            workflowService,
            contextService,
            memoryService,
            monitoringService,
            safetyService,
            orchestrator,
            registry,
            requestService,
            AssistLK.IntegrationTests.TestDoubles.TestAnalysisEvidence.Create(reqRepo));

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

    /// <summary>
    /// Purpose: Checks that a newly created request transitioning to Analyzing recovers back to Created when the agent throws an exception.
    /// Why: Ensures customers can re-attempt analysis if an external AI provider crashes during initial submission.
    /// Scenario: ThrowingAgent throws InvalidOperationException ("External AI service crashed").
    /// Pass condition: Result fails with the exception message, request status recovers to Created, and metric is recorded as Failed.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1FailureTests.Workflow_CreatedToAnalyzing_OnFailure_RecoversToCreated"
    /// </summary>
    [Fact]
    public async Task Workflow_CreatedToAnalyzing_OnFailure_RecoversToCreated()
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

        var throwingAgent = new ThrowingAgent(new InvalidOperationException("External AI service crashed"));
        var registry = new AgentRegistry();
        registry.Register(throwingAgent);
        var orchestrator = new AgentOrchestrator(registry);

        var workflow = new ProblemUnderstandingWorkflowService(
            workflowService,
            contextService,
            memoryService,
            monitoringService,
            safetyService,
            orchestrator,
            registry,
            requestService,
            AssistLK.IntegrationTests.TestDoubles.TestAnalysisEvidence.Create(reqRepo));

        var serviceRequestId = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = serviceRequestId,
            CustomerId = Guid.NewGuid(),
            Description = "Leaking pipe in kitchen",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Created
        });

        var result = await workflow.AnalyzeAsync(new ProblemUnderstandingInput
        {
            ServiceRequestId = serviceRequestId,
            Description = "Leaking pipe in kitchen"
        });

        Assert.False(result.Success);
        Assert.Equal("Failed", result.Outcome);
        Assert.Contains("External AI service crashed", result.ErrorMessage);

        // Status must be restored to Created
        Assert.Equal(ServiceRequestStatus.Created, requests.Single().Status);
        Assert.Empty(analyses);

        var wf = await workflowService.GetAsync(result.WorkflowId);
        Assert.NotNull(wf);
        Assert.Equal("Failed", wf.Status);

        var metric = await db.AgentExecutionMetrics.FirstOrDefaultAsync(x => x.WorkflowId == result.WorkflowId);
        Assert.NotNull(metric);
        Assert.Equal("Failed", metric.Status);
    }

    /// <summary>
    /// Purpose: Checks that a clarification request transitioning to Analyzing recovers back to AwaitingInformation on agent failure.
    /// Why: Preserves customer clarification context instead of resetting the request to Created when follow-up analysis times out.
    /// Scenario: ThrowingAgent throws TimeoutException during clarification re-analysis.
    /// Pass condition: Result fails, request status recovers to AwaitingInformation, and no analysis is saved.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1FailureTests.Workflow_AwaitingInformationToAnalyzing_OnFailure_RecoversToAwaitingInformation"
    /// </summary>
    [Fact]
    public async Task Workflow_AwaitingInformationToAnalyzing_OnFailure_RecoversToAwaitingInformation()
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

        var throwingAgent = new ThrowingAgent(new TimeoutException("Agent timeout"));
        var registry = new AgentRegistry();
        registry.Register(throwingAgent);
        var orchestrator = new AgentOrchestrator(registry);

        var workflow = new ProblemUnderstandingWorkflowService(
            workflowService,
            contextService,
            memoryService,
            monitoringService,
            safetyService,
            orchestrator,
            registry,
            requestService,
            AssistLK.IntegrationTests.TestDoubles.TestAnalysisEvidence.Create(reqRepo));

        var serviceRequestId = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = serviceRequestId,
            CustomerId = Guid.NewGuid(),
            Description = "Need more clarification",
            LocationText = "Kandy",
            Status = ServiceRequestStatus.AwaitingInformation
        });

        var result = await workflow.AnalyzeAsync(new ProblemUnderstandingInput
        {
            ServiceRequestId = serviceRequestId,
            Description = "Need more clarification"
        });

        Assert.False(result.Success);
        Assert.Equal("Failed", result.Outcome);

        // Status must be restored to AwaitingInformation
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, requests.Single().Status);
        Assert.Empty(analyses);

        var wf = await workflowService.GetAsync(result.WorkflowId);
        Assert.NotNull(wf);
        Assert.Equal("Failed", wf.Status);
    }

    /// <summary>
    /// Purpose: Checks that cancelling the cancellation token before or during analysis cleanly recovers request status.
    /// Why: Handles client-side HTTP request cancellations or API gateway timeouts without leaving the request stuck in Analyzing.
    /// Scenario: CancellationTokenSource is pre-cancelled before calling AnalyzeAsync.
    /// Pass condition: Result fails, workflow records Failed, and request status recovers to Created.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1FailureTests.Workflow_CancellationDuringAnalysis_RecoversStateSuccessfully"
    /// </summary>
    [Fact]
    public async Task Workflow_CancellationDuringAnalysis_RecoversStateSuccessfully()
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

        var cancellingAgent = new CancellingAgent();
        var registry = new AgentRegistry();
        registry.Register(cancellingAgent);
        var orchestrator = new AgentOrchestrator(registry);

        var workflow = new ProblemUnderstandingWorkflowService(
            workflowService,
            contextService,
            memoryService,
            monitoringService,
            safetyService,
            orchestrator,
            registry,
            requestService,
            AssistLK.IntegrationTests.TestDoubles.TestAnalysisEvidence.Create(reqRepo));

        var serviceRequestId = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = serviceRequestId,
            CustomerId = Guid.NewGuid(),
            Description = "Water pipe has burst",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Created
        });

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancel token to simulate client cancellation / timeout

        var result = await workflow.AnalyzeAsync(
            new ProblemUnderstandingInput
            {
                ServiceRequestId = serviceRequestId,
                Description = "Water pipe has burst"
            },
            cts.Token);

        Assert.False(result.Success);
        Assert.Equal("Failed", result.Outcome);

        // Status must be recovered back to Created even though token was cancelled
        Assert.Equal(ServiceRequestStatus.Created, requests.Single().Status);
        Assert.Empty(analyses);

        var wf = await workflowService.GetAsync(result.WorkflowId);
        Assert.NotNull(wf);
        Assert.Equal("Failed", wf.Status);
    }

    /// <summary>
    /// Purpose: Checks that if agent analysis finishes successfully just as the HTTP client disconnects, the analysis result is safely persisted.
    /// Why: Prevents throwing away expensive AI reasoning work when an end-user navigates away or disconnects at the final moment.
    /// Scenario: DisconnectingAgent cancels the client token immediately as agent reasoning finishes.
    /// Pass condition: Result succeeds with "Analyzed", status is Analyzed, and ProblemAnalysis entity is safely saved.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1FailureTests.Workflow_CompletedAnalysis_PersistsSafely_EvenIfClientDisconnects"
    /// </summary>
    [Fact]
    public async Task Workflow_CompletedAnalysis_PersistsSafely_EvenIfClientDisconnects()
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

        using var cts = new CancellationTokenSource();

        var validOutput = new ProblemUnderstandingOutput
        {
            Category = "Plumbing",
            ProblemSummary = "Kitchen sink drain pipe is leaking",
            Confidence = 0.95m,
            Urgency = ServiceRequestUrgency.High,
            NeedsMoreInformation = false
        };

        // DisconnectingAgent simulates the client disconnecting (RequestAborted fired)
        // immediately as agent reasoning finishes, before final persistence.
        var disconnectingAgent = new DisconnectingAgent(cts, validOutput);
        var registry = new AgentRegistry();
        registry.Register(disconnectingAgent);
        var orchestrator = new AgentOrchestrator(registry);

        var workflow = new ProblemUnderstandingWorkflowService(
            workflowService,
            contextService,
            memoryService,
            monitoringService,
            safetyService,
            orchestrator,
            registry,
            requestService,
            AssistLK.IntegrationTests.TestDoubles.TestAnalysisEvidence.Create(reqRepo));

        var serviceRequestId = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = serviceRequestId,
            CustomerId = Guid.NewGuid(),
            Description = "Kitchen sink drain pipe is leaking",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Created
        });

        var result = await workflow.AnalyzeAsync(
            new ProblemUnderstandingInput
            {
                ServiceRequestId = serviceRequestId,
                Description = "Kitchen sink drain pipe is leaking"
            },
            cts.Token);

        // Verification for Requirement 5:
        // Completed analysis must be safely persisted rather than discarded solely because RequestAborted was cancelled!
        Assert.True(result.Success);
        Assert.Equal("Analyzed", result.Outcome);
        Assert.Equal(ServiceRequestStatus.Analyzed, requests.Single().Status);
        Assert.Single(analyses);
        Assert.Equal("Kitchen sink drain pipe is leaking", analyses.Single().DetectedProblem);

        var wf = await workflowService.GetAsync(result.WorkflowId);
        Assert.NotNull(wf);
        Assert.Equal("Completed", wf.Status);
    }

    /// <summary>
    /// Purpose: Checks baseline positive workflow execution without any failure or cancellation.
    /// Why: Verifies that failure recovery wrappers do not interfere with normal happy-path analysis completion.
    /// Scenario: FaultyAgent configured with valid electrical problem analysis output.
    /// Pass condition: Result succeeds with "Analyzed", request status is Analyzed, and Category is set to "Electrical".
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1FailureTests.Workflow_SuccessfulAnalysis_BehaviorRemainsUnchanged"
    /// </summary>
    [Fact]
    public async Task Workflow_SuccessfulAnalysis_BehaviorRemainsUnchanged()
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

        var validOutput = new ProblemUnderstandingOutput
        {
            Category = "Electrical",
            ProblemSummary = "Main breaker tripping repeatedly",
            Confidence = 0.9m,
            Urgency = ServiceRequestUrgency.Critical,
            NeedsMoreInformation = false
        };

        var agent = new FaultyAgent(validOutput); // Reuse mock agent with valid output
        var registry = new AgentRegistry();
        registry.Register(agent);
        var orchestrator = new AgentOrchestrator(registry);

        var workflow = new ProblemUnderstandingWorkflowService(
            workflowService,
            contextService,
            memoryService,
            monitoringService,
            safetyService,
            orchestrator,
            registry,
            requestService,
            AssistLK.IntegrationTests.TestDoubles.TestAnalysisEvidence.Create(reqRepo));

        var serviceRequestId = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = serviceRequestId,
            CustomerId = Guid.NewGuid(),
            Description = "Main breaker keeps tripping",
            LocationText = "Kandy",
            Status = ServiceRequestStatus.Created
        });

        var result = await workflow.AnalyzeAsync(new ProblemUnderstandingInput
        {
            ServiceRequestId = serviceRequestId,
            Description = "Main breaker keeps tripping"
        });

        Assert.True(result.Success);
        Assert.Equal("Analyzed", result.Outcome);
        Assert.Equal(ServiceRequestStatus.Analyzed, requests.Single().Status);
        Assert.Equal("Electrical", requests.Single().Category);
        Assert.Single(analyses);
        Assert.Equal("Main breaker tripping repeatedly", analyses.Single().DetectedProblem);

        var wf = await workflowService.GetAsync(result.WorkflowId);
        Assert.NotNull(wf);
        Assert.Equal("Completed", wf.Status);

        var metric = await db.AgentExecutionMetrics.FirstOrDefaultAsync(x => x.WorkflowId == result.WorkflowId);
        Assert.NotNull(metric);
        Assert.Equal("Completed", metric.Status);
    }

    /// <summary>
    /// Purpose: Checks that cancellation triggered mid-way through agent execution recovers a Created request back to Created.
    /// Why: Simulates an abort signal arriving while the agent is executing tools or calling LLM APIs after the request entered Analyzing status.
    /// Scenario: MidExecutionCancellingAgent cancels the token during agent execution.
    /// Pass condition: Result fails, request status recovers from Analyzing back to Created, and metric is recorded as Failed.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1FailureTests.Workflow_MidExecutionCancellation_AfterBeginAnalysis_RecoversToCreated"
    /// </summary>
    [Fact]
    public async Task Workflow_MidExecutionCancellation_AfterBeginAnalysis_RecoversToCreated()
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

        using var cts = new CancellationTokenSource();
        var agent = new MidExecutionCancellingAgent(cts);
        var registry = new AgentRegistry();
        registry.Register(agent);
        var orchestrator = new AgentOrchestrator(registry);

        var workflow = new ProblemUnderstandingWorkflowService(
            workflowService,
            contextService,
            memoryService,
            monitoringService,
            safetyService,
            orchestrator,
            registry,
            requestService,
            AssistLK.IntegrationTests.TestDoubles.TestAnalysisEvidence.Create(reqRepo));

        var serviceRequestId = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = serviceRequestId,
            CustomerId = Guid.NewGuid(),
            Description = "Ceiling leak in bathroom",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Created
        });

        var result = await workflow.AnalyzeAsync(
            new ProblemUnderstandingInput
            {
                ServiceRequestId = serviceRequestId,
                Description = "Ceiling leak in bathroom"
            },
            cts.Token);

        Assert.False(result.Success);
        Assert.Equal("Failed", result.Outcome);

        // Crucial check: status was transitioned to Analyzing, and mid-execution cancellation recovered it to Created
        Assert.Equal(ServiceRequestStatus.Created, requests.Single().Status);
        Assert.Empty(analyses);

        var wf = await workflowService.GetAsync(result.WorkflowId);
        Assert.NotNull(wf);
        Assert.Equal("Failed", wf.Status);

        var metric = await db.AgentExecutionMetrics.FirstOrDefaultAsync(x => x.WorkflowId == result.WorkflowId);
        Assert.NotNull(metric);
        Assert.Equal("Failed", metric.Status);
    }

    /// <summary>
    /// Purpose: Checks that mid-execution cancellation during clarification recovers the request back to AwaitingInformation.
    /// Why: Ensures cancellation during clarification does not demote the request backwards to Created.
    /// Scenario: MidExecutionCancellingAgent cancels the token during re-analysis of an AwaitingInformation request.
    /// Pass condition: Result fails, request status recovers from Analyzing back to AwaitingInformation.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1FailureTests.Workflow_MidExecutionCancellation_WhenAwaitingInformation_RecoversToAwaitingInformation"
    /// </summary>
    [Fact]
    public async Task Workflow_MidExecutionCancellation_WhenAwaitingInformation_RecoversToAwaitingInformation()
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

        using var cts = new CancellationTokenSource();
        var agent = new MidExecutionCancellingAgent(cts);
        var registry = new AgentRegistry();
        registry.Register(agent);
        var orchestrator = new AgentOrchestrator(registry);

        var workflow = new ProblemUnderstandingWorkflowService(
            workflowService,
            contextService,
            memoryService,
            monitoringService,
            safetyService,
            orchestrator,
            registry,
            requestService,
            AssistLK.IntegrationTests.TestDoubles.TestAnalysisEvidence.Create(reqRepo));

        var serviceRequestId = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = serviceRequestId,
            CustomerId = Guid.NewGuid(),
            Description = "Need more details for wiring issue",
            LocationText = "Galle",
            Status = ServiceRequestStatus.AwaitingInformation
        });

        var result = await workflow.AnalyzeAsync(
            new ProblemUnderstandingInput
            {
                ServiceRequestId = serviceRequestId,
                Description = "Need more details for wiring issue"
            },
            cts.Token);

        Assert.False(result.Success);
        Assert.Equal("Failed", result.Outcome);

        // Status was transitioned to Analyzing, and mid-execution cancellation recovered it to AwaitingInformation
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, requests.Single().Status);
        Assert.Empty(analyses);

        var wf = await workflowService.GetAsync(result.WorkflowId);
        Assert.NotNull(wf);
        Assert.Equal("Failed", wf.Status);

        var metric = await db.AgentExecutionMetrics.FirstOrDefaultAsync(x => x.WorkflowId == result.WorkflowId);
        Assert.NotNull(metric);
        Assert.Equal("Failed", metric.Status);
    }

    /// <summary>
    /// Purpose: Checks that when an agent requests more info, the workflow transitions request status to AwaitingInformation with follow-up questions.
    /// Why: Verifies the multi-turn clarification flow properly prompts the customer for missing details.
    /// Scenario: Agent output sets NeedsMoreInformation = true with 2 follow-up questions.
    /// Pass condition: Outcome is "AwaitingInformation", status is AwaitingInformation, and follow-up questions are populated.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1FailureTests.Workflow_SuccessfulAnalysis_WithClarifications_TransitionsToAwaitingInformation"
    /// </summary>
    [Fact]
    public async Task Workflow_SuccessfulAnalysis_WithClarifications_TransitionsToAwaitingInformation()
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

        var clarificationOutput = new ProblemUnderstandingOutput
        {
            Category = "Electrical",
            ProblemSummary = "Power outlet sparking in kitchen",
            Confidence = 0.75m,
            Urgency = ServiceRequestUrgency.High,
            NeedsMoreInformation = true,
            FollowUpQuestions = new List<string>
            {
                "Is there smoke or burning smell?",
                "Which breaker tripped?"
            }
        };

        var agent = new FaultyAgent(clarificationOutput);
        var registry = new AgentRegistry();
        registry.Register(agent);
        var orchestrator = new AgentOrchestrator(registry);

        var workflow = new ProblemUnderstandingWorkflowService(
            workflowService,
            contextService,
            memoryService,
            monitoringService,
            safetyService,
            orchestrator,
            registry,
            requestService,
            AssistLK.IntegrationTests.TestDoubles.TestAnalysisEvidence.Create(reqRepo));

        var serviceRequestId = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = serviceRequestId,
            CustomerId = Guid.NewGuid(),
            Description = "Power outlet sparking",
            LocationText = "Kandy",
            Status = ServiceRequestStatus.Created
        });

        var result = await workflow.AnalyzeAsync(new ProblemUnderstandingInput
        {
            ServiceRequestId = serviceRequestId,
            Description = "Power outlet sparking"
        });

        Assert.True(result.Success);
        Assert.Equal("AwaitingInformation", result.Outcome);
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, requests.Single().Status);
        Assert.Equal("Electrical", requests.Single().Category);
        Assert.True(result.NeedsMoreInformation);
        Assert.Equal(2, result.FollowUpQuestions.Count);
        Assert.Single(analyses);

        var wf = await workflowService.GetAsync(result.WorkflowId);
        Assert.NotNull(wf);
        Assert.Equal("Completed", wf.Status);

        var metric = await db.AgentExecutionMetrics.FirstOrDefaultAsync(x => x.WorkflowId == result.WorkflowId);
        Assert.NotNull(metric);
        Assert.Equal("Completed", metric.Status);
    }

    /// <summary>
    /// Purpose: Checks that if recovery itself throws an error, the original AI failure exception is preserved and not masked.
    /// Why: Critical for diagnostic logging so engineers can see the root cause failure instead of a secondary recovery exception.
    /// Scenario: Agent throws "Original AI processing error" and FaultyRecoveryServiceRequestService throws during recovery.
    /// Pass condition: Result is Failed and ErrorMessage contains "Original AI processing error".
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1FailureTests.Workflow_WhenRecoveryFails_OriginalExceptionIsPreservedAndNotMasked"
    /// </summary>
    [Fact]
    public async Task Workflow_WhenRecoveryFails_OriginalExceptionIsPreservedAndNotMasked()
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
        var requestService = new FaultyRecoveryServiceRequestService(reqRepo, anaRepo);

        var throwingAgent = new ThrowingAgent(new InvalidOperationException("Original AI processing error"));
        var registry = new AgentRegistry();
        registry.Register(throwingAgent);
        var orchestrator = new AgentOrchestrator(registry);

        var workflow = new ProblemUnderstandingWorkflowService(
            workflowService,
            contextService,
            memoryService,
            monitoringService,
            safetyService,
            orchestrator,
            registry,
            requestService,
            AssistLK.IntegrationTests.TestDoubles.TestAnalysisEvidence.Create(reqRepo));

        var serviceRequestId = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = serviceRequestId,
            CustomerId = Guid.NewGuid(),
            Description = "Short circuit in breaker",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Created
        });

        var result = await workflow.AnalyzeAsync(new ProblemUnderstandingInput
        {
            ServiceRequestId = serviceRequestId,
            Description = "Short circuit in breaker"
        });

        // The original exception must be preserved in result.ErrorMessage rather than masked by recovery error
        Assert.False(result.Success);
        Assert.Equal("Failed", result.Outcome);
        Assert.Contains("Original AI processing error", result.ErrorMessage);

        var wf = await workflowService.GetAsync(result.WorkflowId);
        Assert.NotNull(wf);
        Assert.Equal("Failed", wf.Status);

        var metric = await db.AgentExecutionMetrics.FirstOrDefaultAsync(x => x.WorkflowId == result.WorkflowId);
        Assert.NotNull(metric);
        Assert.Equal("Failed", metric.Status);
    }

    private sealed class FaultyRecoveryServiceRequestService : ServiceRequestService
    {
        public FaultyRecoveryServiceRequestService(
            IServiceRequestRepository serviceRequestRepository,
            IProblemAnalysisRepository problemAnalysisRepository)
            : base(serviceRequestRepository, problemAnalysisRepository)
        {
        }

        public override Task<ServiceRequestResponse> RecoverFailedAnalysisAsync(
            Guid serviceRequestId,
            ServiceRequestStatus previousStatus,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Database connection failure during recovery");
        }
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

    private sealed class ThrowingAgent : AssistLK.Agents.Abstractions.IAgent
    {
        private readonly Exception _exception;
        public ThrowingAgent(Exception exception) => _exception = exception;
        public string Name => "ProblemUnderstandingAgent";
        public Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default)
            => throw _exception;
    }

    private sealed class CancellingAgent : AssistLK.Agents.Abstractions.IAgent
    {
        public string Name => "ProblemUnderstandingAgent";
        public Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private sealed class MidExecutionCancellingAgent : AssistLK.Agents.Abstractions.IAgent
    {
        private readonly CancellationTokenSource _cts;
        public MidExecutionCancellingAgent(CancellationTokenSource cts) => _cts = cts;
        public string Name => "ProblemUnderstandingAgent";
        public Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default)
        {
            _cts.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private sealed class DisconnectingAgent : AssistLK.Agents.Abstractions.IAgent
    {
        private readonly CancellationTokenSource _cts;
        private readonly ProblemUnderstandingOutput _output;
        public DisconnectingAgent(CancellationTokenSource cts, ProblemUnderstandingOutput output)
        {
            _cts = cts;
            _output = output;
        }
        public string Name => "ProblemUnderstandingAgent";
        public Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default)
        {
            _cts.Cancel();
            return Task.FromResult(new AgentResult
            {
                Success = true,
                Message = "Analysis succeeded before disconnect",
                Data = _output
            });
        }
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

        public Task<IReadOnlyList<ServiceRequest>> GetAllForAdminAsync(
            ServiceRequestStatus? status = null,
            string? category = null,
            ServiceRequestUrgency? urgency = null,
            CancellationToken cancellationToken = default)
        {
            var query = _requests.AsEnumerable();
            if (status.HasValue) query = query.Where(x => x.Status == status.Value);
            if (!string.IsNullOrWhiteSpace(category)) query = query.Where(x => string.Equals(x.Category, category, StringComparison.OrdinalIgnoreCase));
            if (urgency.HasValue) query = query.Where(x => x.Urgency == urgency.Value);
            return Task.FromResult<IReadOnlyList<ServiceRequest>>(query.OrderByDescending(x => x.CreatedAt).ToArray());
        }

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
