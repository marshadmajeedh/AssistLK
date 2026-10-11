namespace AssistLK.Domain.Entities;

public class AgentApproval
{
    public Guid Id { get; set; }

    public Guid WorkflowId { get; set; }

    public string Action { get; set; }
        = string.Empty;

    public string Status { get; set; }
        = "Pending";

    public Guid? ApprovedByUserId { get; set; }

    public string? DecisionReason { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? DecidedAtUtc { get; set; }

    public AgentWorkflow Workflow { get; set; }
        = null!;
}