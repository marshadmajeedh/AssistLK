using AssistLK.Application.Services;
using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.IntegrationTests;

public class Component1MonitoringTests
{
    private static AssistLKDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AssistLKDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AssistLKDbContext(options);
    }

    [Fact]
    public async Task MonitoringService_RecordsSuccessfulExecutionWithActualToolCalls()
    {
        await using var db = CreateDb();
        var monitoring = new AgentMonitoringService(db);

        var workflowId = Guid.NewGuid();
        var executionId = Guid.NewGuid();

        await monitoring.RecordAsync(
            workflowId,
            executionId,
            "ProblemUnderstandingAgent",
            "Completed",
            executionTimeMs: 145,
            toolCalls: 3);

        var metrics = await db.AgentExecutionMetrics.ToListAsync();
        Assert.Single(metrics);

        var m = metrics.Single();
        Assert.Equal(workflowId, m.WorkflowId);
        Assert.Equal(executionId, m.ExecutionId);
        Assert.Equal("ProblemUnderstandingAgent", m.AgentName);
        Assert.Equal("Completed", m.Status);
        Assert.Equal(145, m.ExecutionTimeMs);
        Assert.Equal(3, m.ToolCalls);
        Assert.True(m.CreatedAtUtc <= DateTime.UtcNow);
    }

    [Fact]
    public async Task MonitoringService_RecordsFailedExecutionWithZeroToolCalls()
    {
        await using var db = CreateDb();
        var monitoring = new AgentMonitoringService(db);

        var workflowId = Guid.NewGuid();
        var executionId = Guid.NewGuid();

        await monitoring.RecordAsync(
            workflowId,
            executionId,
            "ProblemUnderstandingAgent",
            "Failed",
            executionTimeMs: 50,
            toolCalls: 0);

        var metrics = await db.AgentExecutionMetrics.ToListAsync();
        Assert.Single(metrics);

        var m = metrics.Single();
        Assert.Equal("Failed", m.Status);
        Assert.Equal(0, m.ToolCalls);
        Assert.Equal(50, m.ExecutionTimeMs);
    }

    [Fact]
    public async Task MonitoringService_TracksMultipleExecutionsSeparately()
    {
        await using var db = CreateDb();
        var monitoring = new AgentMonitoringService(db);

        var wf1 = Guid.NewGuid();
        var wf2 = Guid.NewGuid();

        await monitoring.RecordAsync(wf1, Guid.NewGuid(), "ProblemUnderstandingAgent", "Completed", 100, 3);
        await monitoring.RecordAsync(wf2, Guid.NewGuid(), "ProblemUnderstandingAgent", "Completed", 120, 3);

        var metrics = await db.AgentExecutionMetrics.ToListAsync();
        Assert.Equal(2, metrics.Count);
        Assert.Contains(metrics, m => m.WorkflowId == wf1);
        Assert.Contains(metrics, m => m.WorkflowId == wf2);
    }
}
