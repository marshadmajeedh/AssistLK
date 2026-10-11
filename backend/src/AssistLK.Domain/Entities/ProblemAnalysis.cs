namespace AssistLK.Domain.Entities;

public class ProblemAnalysis : BaseEntity
{
    public ProblemAnalysisVisualEvidence VisualEvidence { get; set; } = new();

    public long EvidenceRevision { get; set; } = 1;

    public Guid ServiceRequestId { get; set; }

    public ServiceRequest ServiceRequest { get; set; } = null!;

    public string DetectedProblem { get; set; } = string.Empty;

    public decimal Confidence { get; set; }

    public string AgentName { get; set; } = string.Empty;
}