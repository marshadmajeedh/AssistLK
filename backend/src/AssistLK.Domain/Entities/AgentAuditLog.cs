namespace AssistLK.Domain.Entities;

public class AgentAuditLog
{
    public Guid Id { get; set; }

    public Guid WorkflowId { get; set; }

    public Guid? UserId { get; set; }

    public string EventType { get; set; }
        = string.Empty;

    public string Description { get; set; }
        = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public AgentWorkflow Workflow { get; set; }
        = null!;
}