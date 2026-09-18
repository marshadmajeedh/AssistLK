namespace AssistLK.Application.ServiceRequests.DTOs;

/// <summary>
/// Authoritative summary of persisted ProblemAnalysis.
/// Exposes only the fields actually stored on the ProblemAnalysis entity.
/// </summary>
public class ProblemAnalysisSummaryDto
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public AssistLK.Domain.Entities.ProblemAnalysisVisualEvidence? VisualEvidence { get; set; }

    public Guid Id { get; set; }

    public string DetectedProblem { get; set; } = string.Empty;

    public decimal Confidence { get; set; }

    public string AgentName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
