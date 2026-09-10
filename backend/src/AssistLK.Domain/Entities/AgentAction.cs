namespace AssistLK.Domain.Entities;

public class AgentAction
{
    public Guid Id { get; set; }

    public Guid WorkflowId { get; set; }

    public string ActionType { get; set; }
        = string.Empty;

    public string Description { get; set; }
        = string.Empty;

    public string RiskLevel { get; set; }
        = string.Empty;

    public string Status { get; set; }
        = "Pending";

    public bool RequiresApproval { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public AgentWorkflow Workflow { get; set; }
        = null!;
}