namespace AssistLK.Application.ServiceRequests.DTOs;

public class ProblemAnalysisResponse
{
    public Guid ProblemAnalysisId { get; set; }
    public Guid ServiceRequestId { get; set; }
    public string DetectedProblem { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
