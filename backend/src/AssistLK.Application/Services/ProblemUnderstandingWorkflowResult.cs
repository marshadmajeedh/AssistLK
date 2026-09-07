using AssistLK.Agents.Models;

namespace AssistLK.Application.Services;

/// <summary>
/// Result of executing the Problem Understanding agent workflow.
/// </summary>
public class ProblemUnderstandingWorkflowResult
{
    public bool Success { get; set; }

    public Guid WorkflowId { get; set; }

    public Guid ExecutionId { get; set; }

    /// <summary>
    /// High-level outcome of the workflow: "Analyzed", "AwaitingInformation", or "Failed".
    /// </summary>
    public string Outcome { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public ProblemUnderstandingOutput? Output { get; set; }
}
