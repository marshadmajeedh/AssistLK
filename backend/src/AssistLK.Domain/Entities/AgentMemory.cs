namespace AssistLK.Domain.Entities;

public class AgentMemory
{
    public Guid Id { get; set; }

    public Guid WorkflowId { get; set; }

    public string Key { get; set; }
        = string.Empty;

    public string Value { get; set; }
        = string.Empty;

    public string SourceAgent { get; set; }
        = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public AgentWorkflow Workflow { get; set; }
        = null!;
}
