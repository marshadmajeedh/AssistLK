namespace AssistLK.Domain.Entities;

public class AgentExecution
{
    public Guid Id { get; set; }

    public Guid WorkflowId { get; set; }

    public string AgentName { get; set; }
        = string.Empty;

    public string Status { get; set; }
        = "Started";

    public string? Input { get; set; }

    public string? Output { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public AgentWorkflow Workflow { get; set; }
        = null!;
}