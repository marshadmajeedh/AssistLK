using AssistLK.Application.Services;
using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.IntegrationTests;

/// <summary>
/// What this class tests:
///   Telemetry, execution latency tracking, status recording, and tool call count metrics for agent executions in Component 1.
/// Why these tests are needed:
///   Ensures the operations team has accurate observability into agent latency, tool usage, failure rates, and execution status.
/// What is real and what is mocked/faked:
///   - Real: AgentMonitoringService running against an EF Core In-Memory database.
///   - Mocked/Faked: None. Uses real EF Core in-memory context.
/// Prerequisites:
///   None. Runs completely in-memory without external services or network calls.
/// Run from repository root:
///   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.Component1MonitoringTests"
/// </summary>
public class Component1MonitoringTests
{
    private static AssistLKDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AssistLKDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AssistLKDbContext(options);
    }

    /// <summary>
    /// Purpose: Checks that successful agent execution records latency (145ms), status ("Completed"), and actual tool call count (3).
    /// Why: Verifies operational observability into successful problem understanding workflows.
    /// Scenario: Records execution metrics for ProblemUnderstandingAgent with status "Completed" and 3 tool calls.
    /// Pass condition: DB contains a single metric record with matching workflow ID, execution ID, status, latency, and tool count.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1MonitoringTests.MonitoringService_RecordsSuccessfulExecutionWithActualToolCalls"
    /// </summary>
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

    /// <summary>
    /// Purpose: Checks that failed executions record failure status with zero tool calls and recorded duration.
    /// Why: Ensures failures occurring prior to tool execution are tracked with accurate 0 tool call counts.
    /// Scenario: Records metric with status "Failed", executionTimeMs = 50, and toolCalls = 0.
    /// Pass condition: Stored metric Status is "Failed", ToolCalls is 0, and ExecutionTimeMs is 50.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1MonitoringTests.MonitoringService_RecordsFailedExecutionWithZeroToolCalls"
    /// </summary>
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

    /// <summary>
    /// Purpose: Checks that multiple executions across different workflows are tracked independently.
    /// Why: Confirms telemetry is segregated correctly per workflow without cross-over.
    /// Scenario: Records metrics for 2 different workflow IDs.
    /// Pass condition: Metrics table has 2 records, each matching its respective workflow ID.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1MonitoringTests.TracksMultipleExecutionsSeparately"
    /// </summary>
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
