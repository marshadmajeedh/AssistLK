namespace AssistLK.Domain.Entities;

public class AgentExecutionMetric
{
    public Guid Id { get; set; }

    public Guid WorkflowId { get; set; }

    public Guid? ExecutionId { get; set; }

    public string AgentName { get; set; }
        = string.Empty;

    public string Status { get; set; }
        = string.Empty;

    public long ExecutionTimeMs { get; set; }

    public int ToolCalls { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public AgentWorkflow Workflow { get; set; }
        = null!;
}