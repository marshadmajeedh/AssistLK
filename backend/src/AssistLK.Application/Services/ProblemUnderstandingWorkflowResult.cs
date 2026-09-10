using AssistLK.Agents.Models;
using AssistLK.Domain.Enums;

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

    public Guid ServiceRequestId { get; set; }
    public ServiceRequestStatus Status { get; set; }
    public string Category { get; set; } = string.Empty;
    public string ProblemSummary { get; set; } = string.Empty;
    public ServiceRequestUrgency Urgency { get; set; }
    public decimal Confidence { get; set; }
    public bool NeedsMoreInformation { get; set; }
    public IReadOnlyList<string> FollowUpQuestions { get; set; } = Array.Empty<string>();
}
