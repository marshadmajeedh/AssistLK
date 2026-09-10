namespace AssistLK.Domain.Entities;

public class AgentWorkflow
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public string WorkflowType { get; set; }
        = string.Empty;

    public string Status { get; set; }
        = "Pending";

    public string? CurrentAgent { get; set; }

    public string? Input { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<AgentExecution> Executions { get; set; }
        = new List<AgentExecution>();

    public ICollection<AgentApproval> Approvals { get; set; }
        = new List<AgentApproval>();

    public ICollection<AgentAuditLog> AuditLogs { get; set; }
        = new List<AgentAuditLog>();
}