using AssistLK.Application.Interfaces;
using AssistLK.Domain.Entities;

namespace AssistLK.Application.Services;

public class AgentMonitoringService
{
    private readonly IAgentWorkflowDbContext _db;

    public AgentMonitoringService(
        IAgentWorkflowDbContext db)
    {
        _db = db;
    }

    public async Task RecordAsync(
        Guid workflowId,
        Guid executionId,
        string agentName,
        string status,
        long executionTimeMs,
        int toolCalls)
    {
        var metric =
            new AgentExecutionMetric
            {
                Id = Guid.NewGuid(),

                WorkflowId =
                    workflowId,

                ExecutionId =
                    executionId,

                AgentName =
                    agentName,

                Status =
                    status,

                ExecutionTimeMs =
                    executionTimeMs,

                ToolCalls =
                    toolCalls,

                CreatedAtUtc =
                    DateTime.UtcNow
            };

        _db.AgentExecutionMetrics.Add(metric);

        await _db.SaveChangesAsync();
    }
}