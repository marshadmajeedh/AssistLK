using AssistLK.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.IntegrationTests.PostgreSql;

public class WorkflowAndMonitoringPostgreSqlTests : PostgreSqlIntegrationTestBase
{
    public WorkflowAndMonitoringPostgreSqlTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

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
