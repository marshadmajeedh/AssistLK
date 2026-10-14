using AssistLK.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.IntegrationTests.PostgreSql;

// =========================================================================================
// Class: WorkflowAndMonitoringPostgreSqlTests
// What this class tests:
//   PostgreSQL persistence and foreign key cascade deletion for AgentWorkflow and its child
//   telemetry entities (AgentExecution, AgentExecutionMetric, AgentApproval, AgentAuditLog, AgentAction).
// Why these tests are needed:
//   Ensures that agent orchestration and audit telemetry records are properly persisted in PostgreSQL,
//   and that deleting an AgentWorkflow automatically cascades to clean up all related child rows
//   without leaving orphaned audit records or triggering foreign key constraint violations.
// What is real vs mocked/faked:
//   - REAL: Live PostgreSQL instance, foreign key cascade constraints, and EF Core entity mappings.
//   - MOCKED: None.
// Prerequisites:
//   A running PostgreSQL database instance configured via ASSISTLK_TEST_POSTGRESQL_CONNECTION,
//   ConnectionStrings__TestConnection, or local User Secrets.
// Run from repository root:
//   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.PostgreSql.WorkflowAndMonitoringPostgreSqlTests"
// =========================================================================================
public class WorkflowAndMonitoringPostgreSqlTests : PostgreSqlIntegrationTestBase
{
    public WorkflowAndMonitoringPostgreSqlTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    // Purpose: Verifies that an AgentWorkflow and all 5 child telemetry entities persist in PostgreSQL and that deleting the parent cascades to delete all child rows.
    // Why: Verifies schema integrity for agent observability tables; deleting obsolete workflows must clean up child tables without orphaned rows.
    // Scenario: Creates a workflow and one of each child entity (AgentExecution, AgentExecutionMetric, AgentApproval, AgentAuditLog, AgentAction), asserts persistence, deletes workflow, and asserts all child tables are empty for that workflow.
    // Pass condition: Initial insert succeeds and queries return 1 of each entity; post-delete queries for all 5 child tables return empty collections.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.PostgreSql.WorkflowAndMonitoringPostgreSqlTests.AgentWorkflow_AndChildEntities_PersistAndCascadeDelete_InPostgreSql"
    [Fact]
    public async Task AgentWorkflow_AndChildEntities_PersistAndCascadeDelete_InPostgreSql()
    {
        var workflowId = Guid.NewGuid();

        await using (var context = CreateDbContext())
        {
            var workflow = new AgentWorkflow
            {
                Id = workflowId,
                WorkflowType = "ProblemUnderstanding",
                Status = "Completed",
                CurrentAgent = "ProblemUnderstandingAgent",
                Input = "Customer problem statement",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            var execution = new AgentExecution
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflowId,
                AgentName = "ProblemUnderstandingAgent",
                Status = "Success",
                Input = "input text",
                Output = "output text",
                StartedAtUtc = DateTime.UtcNow.AddSeconds(-2),
                CompletedAtUtc = DateTime.UtcNow
            };

            var metric = new AgentExecutionMetric
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflowId,
                AgentName = "ProblemUnderstandingAgent",
                ExecutionTimeMs = 1250,
                ToolCalls = 3,
                Status = "Success",
                CreatedAtUtc = DateTime.UtcNow
            };

            var approval = new AgentApproval
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflowId,
                Action = "AutoClassification",
                Status = "Approved",
                DecisionReason = "High confidence classification",
                CreatedAtUtc = DateTime.UtcNow
            };

            var auditLog = new AgentAuditLog
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflowId,
                EventType = "ExecutionCompleted",
                Description = "Workflow executed successfully with 3 tool invocations",
                CreatedAtUtc = DateTime.UtcNow
            };

            var action = new AgentAction
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflowId,
                ActionType = "SafetyCheck",
                Description = "Verified no life safety hazards",
                RiskLevel = "Low",
                Status = "Completed",
                CreatedAtUtc = DateTime.UtcNow
            };

            await context.AgentWorkflows.AddAsync(workflow);
            await context.AgentExecutions.AddAsync(execution);
            await context.AgentExecutionMetrics.AddAsync(metric);
            await context.AgentApprovals.AddAsync(approval);
            await context.AgentAuditLogs.AddAsync(auditLog);
            await context.AgentActions.AddAsync(action);

            await context.SaveChangesAsync();
        }

        // Verify entities exist
        await using (var verifyContext = CreateDbContext())
        {
            var wf = await verifyContext.AgentWorkflows
                .Include(w => w.Executions)
                .Include(w => w.Approvals)
                .Include(w => w.AuditLogs)
                .FirstOrDefaultAsync(w => w.Id == workflowId);

            Assert.NotNull(wf);
            Assert.Single(wf.Executions);
            Assert.Single(wf.Approvals);
            Assert.Single(wf.AuditLogs);

            var metricCount = await verifyContext.AgentExecutionMetrics.CountAsync(m => m.WorkflowId == workflowId);
            Assert.Equal(1, metricCount);

            var actionCount = await verifyContext.AgentActions.CountAsync(a => a.WorkflowId == workflowId);
            Assert.Equal(1, actionCount);
        }

        // Delete workflow and verify cascade delete
        await using (var deleteContext = CreateDbContext())
        {
            var wf = await deleteContext.AgentWorkflows.FindAsync(workflowId);
            Assert.NotNull(wf);

            deleteContext.AgentWorkflows.Remove(wf);
            await deleteContext.SaveChangesAsync();
        }

        // Verify cascade deletion of children
        await using (var afterDeleteContext = CreateDbContext())
        {
            Assert.Empty(await afterDeleteContext.AgentExecutions.Where(e => e.WorkflowId == workflowId).ToListAsync());
            Assert.Empty(await afterDeleteContext.AgentApprovals.Where(a => a.WorkflowId == workflowId).ToListAsync());
            Assert.Empty(await afterDeleteContext.AgentAuditLogs.Where(l => l.WorkflowId == workflowId).ToListAsync());
            Assert.Empty(await afterDeleteContext.AgentExecutionMetrics.Where(m => m.WorkflowId == workflowId).ToListAsync());
            Assert.Empty(await afterDeleteContext.AgentActions.Where(a => a.WorkflowId == workflowId).ToListAsync());
        }
    }
}
