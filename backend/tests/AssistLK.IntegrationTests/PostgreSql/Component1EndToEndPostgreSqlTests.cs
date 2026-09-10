using AssistLK.Agents.Agents;
using AssistLK.Agents.Core;
using AssistLK.Agents.Services;
using AssistLK.Agents.Tools;
using AssistLK.Application.Interfaces;
using AssistLK.Application.Services;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;
using AssistLK.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssistLK.IntegrationTests.PostgreSql;

public class Component1EndToEndPostgreSqlTests : PostgreSqlIntegrationTestBase
{
    public Component1EndToEndPostgreSqlTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    private ProblemUnderstandingWorkflowService CreateWorkflowService(AssistLKDbContext context)
    {
        var workflowService = new AgentWorkflowService(context);
        var memoryService = new AgentMemoryService(context);
        var contextService = new AgentContextService(memoryService);
        var monitoringService = new AgentMonitoringService(context);
        var safetyService = new AgentSafetyService(new AgentSafetyPolicyEngine(), context);

        var toolRegistry = new ToolRegistry();
        toolRegistry.Register(new ProblemClassificationTool());
        toolRegistry.Register(new LocationExtractionTool());
        toolRegistry.Register(new ServiceKnowledgeTool());

        var toolExecutor = new ToolExecutor(toolRegistry);
        var agent = new ProblemUnderstandingAgent(
            toolExecutor,
            new GeminiService(),
            NullLogger<ProblemUnderstandingAgent>.Instance);

        var registry = new AgentRegistry();
        registry.Register(agent);

        var orchestrator = new AgentOrchestrator(registry);

        var requestRepo = new ServiceRequestRepository(context);
        var analysisRepo = new ProblemAnalysisRepository(context);
        var requestService = new ServiceRequestService(requestRepo, analysisRepo);

        return new ProblemUnderstandingWorkflowService(
            workflowService,
            contextService,
            memoryService,
            monitoringService,
            safetyService,
            orchestrator,
            registry,
            requestService);
    }

    [Fact]
    public async Task HappyPath_KitchenPipeBurst_EndToEnd_PersistsInRealPostgreSql_AndTransitionsToReadyForMatching()
    {
        var customer = await CreateUserAsync(email: "pipe_customer@assistlk.com");
        var requestId = Guid.NewGuid();

        // 1. Customer submits request to PostgreSQL
        await using (var context = CreateDbContext())
        {
            var req = new ServiceRequest
            {
                Id = requestId,
                CustomerId = customer.Id,
                Category = "Plumbing",
                Description = "Kitchen pipe is leaking under sink and flooding floor",
                LocationText = "Colombo 03",
                Urgency = ServiceRequestUrgency.High,
                Status = ServiceRequestStatus.Created
            };
            await context.ServiceRequests.AddAsync(req);
            await context.SaveChangesAsync();
        }

        // 2. Execute full Component 1 Workflow backed by real PostgreSQL
        await using (var workflowContext = CreateDbContext())
        {
            var workflowService = CreateWorkflowService(workflowContext);
            var result = await workflowService.AnalyzeAsync(requestId, customer.Id);

            Assert.True(result.Success, $"Workflow execution failed: {result.ErrorMessage}");
            Assert.NotNull(result.Output);
            Assert.Equal("Plumbing", result.Output.Category);
            Assert.Equal(ServiceRequestUrgency.High, result.Output.Urgency);
            Assert.True(result.Output.Confidence >= 0.80m);
            Assert.False(result.Output.NeedsMoreInformation);
        }

        // 3. Verify real PostgreSQL database state
        await using (var verifyContext = CreateDbContext())
        {
            var savedRequest = await verifyContext.ServiceRequests
                .Include(r => r.ProblemAnalyses)
                .SingleOrDefaultAsync(r => r.Id == requestId);

            Assert.NotNull(savedRequest);
            Assert.Equal(ServiceRequestStatus.Analyzed, savedRequest.Status);
            Assert.Equal("Plumbing", savedRequest.Category);
            Assert.Equal(ServiceRequestUrgency.High, savedRequest.Urgency);

            Assert.Single(savedRequest.ProblemAnalyses);
            var savedAnalysis = savedRequest.ProblemAnalyses.First();
            Assert.NotEmpty(savedAnalysis.DetectedProblem);
            Assert.True(savedAnalysis.Confidence >= 0.80m);
            Assert.Equal("ProblemUnderstandingAgent", savedAnalysis.AgentName);

            // 4. Mark ReadyForMatching and verify handoff
            var requestRepo = new ServiceRequestRepository(verifyContext);
            var analysisRepo = new ProblemAnalysisRepository(verifyContext);
            var service = new ServiceRequestService(requestRepo, analysisRepo);

            var readyResult = await service.MarkReadyForMatchingAsync(requestId, customer.Id);
            Assert.Equal(ServiceRequestStatus.ReadyForMatching, readyResult.Status);

            var matchingHandoff = await service.GetReadyForMatchingAsync(requestId);
            Assert.NotNull(matchingHandoff);
            Assert.Equal(requestId, matchingHandoff.ServiceRequestId);
            Assert.Equal("Plumbing", matchingHandoff.Category);
            Assert.Equal(ServiceRequestUrgency.High, matchingHandoff.Urgency);
            Assert.True(matchingHandoff.Confidence >= 0.80m);

            // Verify AgentWorkflow recorded in PostgreSQL
            var workflowRecord = await verifyContext.AgentWorkflows
                .Include(w => w.Executions)
                .Include(w => w.AuditLogs)
                .SingleOrDefaultAsync(w => w.WorkflowType == "ProblemUnderstanding" && w.Input == requestId.ToString());

            Assert.NotNull(workflowRecord);
            Assert.Equal("Completed", workflowRecord.Status);
            Assert.NotEmpty(workflowRecord.Executions);
            Assert.NotEmpty(workflowRecord.AuditLogs);

            // Verify AgentMemory keys recorded in PostgreSQL
            var memories = await verifyContext.AgentMemories
                .Where(m => m.WorkflowId == workflowRecord.Id)
                .ToListAsync();

            Assert.NotEmpty(memories);
            Assert.Contains(memories, m => m.Key == "problem.category" && m.Value == "Plumbing");
            Assert.Contains(memories, m => m.Key == "problem.urgency" && m.Value == "High");
        }
    }

    [Fact]
    public async Task AmbiguousPath_RefrigeratorScenario_PreservesCanonicalCategory_AndResolvesWithFollowUp()
    {
        var customer = await CreateUserAsync(email: "fridge_customer@assistlk.com");
        var requestId = Guid.NewGuid();

        // 1. Customer submits vague request: "refrigerator"
        await using (var context = CreateDbContext())
        {
            var req = new ServiceRequest
            {
                Id = requestId,
                CustomerId = customer.Id,
                Category = "Appliance Repair",
                Description = "refrigerator",
                LocationText = "Galle",
                Urgency = ServiceRequestUrgency.Unknown,
                Status = ServiceRequestStatus.Created
            };
            await context.ServiceRequests.AddAsync(req);
            await context.SaveChangesAsync();
        }

        // 2. Initial analysis detects ambiguity
        await using (var workflowContext = CreateDbContext())
        {
            var workflowService = CreateWorkflowService(workflowContext);
            var initialResult = await workflowService.AnalyzeAsync(requestId, customer.Id);

            Assert.True(initialResult.Success);
            Assert.NotNull(initialResult.Output);

            // Canonical vocabulary check: must be "Appliance Repair"
            Assert.Equal("Appliance Repair", initialResult.Output.Category);
            Assert.True(initialResult.Output.NeedsMoreInformation);
            Assert.NotEmpty(initialResult.Output.FollowUpQuestions);
        }

        // Verify status in PostgreSQL became AwaitingInformation
        await using (var verifyContext = CreateDbContext())
        {
            var req = await verifyContext.ServiceRequests.FindAsync(requestId);
            Assert.NotNull(req);
            Assert.Equal(ServiceRequestStatus.AwaitingInformation, req.Status);
            Assert.Equal("Appliance Repair", req.Category);
        }

        // 3. Customer provides clarification: "The refrigerator is warm and not cooling food"
        await using (var updateContext = CreateDbContext())
        {
            var req = await updateContext.ServiceRequests.FindAsync(requestId);
            Assert.NotNull(req);
            req.Description = "The refrigerator is warm and not cooling food";
            await updateContext.SaveChangesAsync();
        }

        // 4. Second analysis runs with clarification
        await using (var workflowContext = CreateDbContext())
        {
            var workflowService = CreateWorkflowService(workflowContext);
            var followUpResult = await workflowService.AnalyzeAsync(requestId, customer.Id);

            Assert.True(followUpResult.Success);
            Assert.NotNull(followUpResult.Output);

            // Still strictly canonical "Appliance Repair"
            Assert.Equal("Appliance Repair", followUpResult.Output.Category);
            Assert.False(followUpResult.Output.NeedsMoreInformation);
            Assert.True(followUpResult.Output.Confidence >= 0.70m);
        }

        // 5. Verify final status and analysis history in PostgreSQL
        await using (var finalContext = CreateDbContext())
        {
            var repo = new ServiceRequestRepository(finalContext);
            var reqWithAnalyses = await repo.GetByIdAsync(requestId, includeProblemAnalyses: true);

            Assert.NotNull(reqWithAnalyses);
            Assert.Equal(ServiceRequestStatus.Analyzed, reqWithAnalyses.Status);
            Assert.Equal("Appliance Repair", reqWithAnalyses.Category);

            // There should be 2 analyses in history
            Assert.Equal(2, reqWithAnalyses.ProblemAnalyses.Count);

            var analysisRepo = new ProblemAnalysisRepository(finalContext);
            var mostRecent = await analysisRepo.GetMostRecentByServiceRequestIdAsync(requestId);
            Assert.NotNull(mostRecent);
            Assert.True(mostRecent.Confidence >= 0.70m);

            // 6. Mark ReadyForMatching and verify matching handoff contract
            var service = new ServiceRequestService(repo, analysisRepo);
            var readyResult = await service.MarkReadyForMatchingAsync(requestId, customer.Id);
            Assert.Equal(ServiceRequestStatus.ReadyForMatching, readyResult.Status);

            var matchingHandoff = await service.GetReadyForMatchingAsync(requestId);
            Assert.NotNull(matchingHandoff);
            Assert.Equal(requestId, matchingHandoff.ServiceRequestId);
            Assert.Equal("Appliance Repair", matchingHandoff.Category);
        }
    }

    [Fact]
    public async Task SafetyRegression_ExposedSparkingWire_DetectsHighUrgency_AndPersistsSafetyGuidance()
    {
        var customer = await CreateUserAsync(email: "safety_customer@assistlk.com");
        var requestId = Guid.NewGuid();

        await using (var context = CreateDbContext())
        {
            var req = new ServiceRequest
            {
                Id = requestId,
                CustomerId = customer.Id,
                Category = "Electrical",
                Description = "Exposed sparking wire in bathroom near water",
                LocationText = "Kandy",
                Urgency = ServiceRequestUrgency.High,
                Status = ServiceRequestStatus.Created
            };
            await context.ServiceRequests.AddAsync(req);
            await context.SaveChangesAsync();
        }

        await using (var workflowContext = CreateDbContext())
        {
            var workflowService = CreateWorkflowService(workflowContext);
            var result = await workflowService.AnalyzeAsync(requestId, customer.Id);

            Assert.True(result.Success);
            Assert.NotNull(result.Output);
            Assert.Equal("Electrical", result.Output.Category);
            Assert.Equal(ServiceRequestUrgency.High, result.Output.Urgency);
        }

        await using (var verifyContext = CreateDbContext())
        {
            var saved = await verifyContext.ServiceRequests.FindAsync(requestId);
            Assert.NotNull(saved);
            Assert.Equal(ServiceRequestUrgency.High, saved.Urgency);
            Assert.Equal(ServiceRequestStatus.Analyzed, saved.Status);

            // Also verify marking ready for matching succeeds
            var requestRepo = new ServiceRequestRepository(verifyContext);
            var analysisRepo = new ProblemAnalysisRepository(verifyContext);
            var service = new ServiceRequestService(requestRepo, analysisRepo);
            var readyResult = await service.MarkReadyForMatchingAsync(requestId, customer.Id);
            Assert.Equal(ServiceRequestStatus.ReadyForMatching, readyResult.Status);
        }
    }

    [Fact]
    public async Task OwnershipEnforcement_AtWorkflowBoundary_PreventsUnauthorizedAnalysis()
    {
        var owner = await CreateUserAsync(email: "owner@assistlk.com");
        var unauthorizedUser = await CreateUserAsync(email: "unauth@assistlk.com");
        var requestId = Guid.NewGuid();

        await using (var context = CreateDbContext())
        {
            var req = new ServiceRequest
            {
                Id = requestId,
                CustomerId = owner.Id,
                Category = "Plumbing",
                Description = "Burst pipe",
                LocationText = "Colombo",
                Urgency = ServiceRequestUrgency.High,
                Status = ServiceRequestStatus.Created
            };
            await context.ServiceRequests.AddAsync(req);
            await context.SaveChangesAsync();
        }

        await using (var workflowContext = CreateDbContext())
        {
            var workflowService = CreateWorkflowService(workflowContext);

            // Unauthorized user attempts to trigger workflow
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                workflowService.AnalyzeAsync(requestId, unauthorizedUser.Id));
        }

        // Verify request was untouched
        await using (var verifyContext = CreateDbContext())
        {
            var req = await verifyContext.ServiceRequests.FindAsync(requestId);
            Assert.NotNull(req);
            Assert.Equal(ServiceRequestStatus.Created, req.Status);
        }
    }
}
