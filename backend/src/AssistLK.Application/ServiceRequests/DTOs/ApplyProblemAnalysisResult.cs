using System.ComponentModel.DataAnnotations;
using AssistLK.Domain.Enums;

namespace AssistLK.Application.ServiceRequests.DTOs;

public class ApplyProblemAnalysisResult
{
    public AssistLK.Domain.Entities.ProblemAnalysisVisualEvidence VisualEvidence { get; set; } = new();

    public IReadOnlyList<Guid> SuppliedAttachmentIds { get; set; } = Array.Empty<Guid>();

    public long? EvidenceRevision { get; set; }

    public Guid ServiceRequestId { get; set; }

    [Required]
    [MaxLength(2000)]
    public string DetectedProblem { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    public ServiceRequestUrgency Urgency { get; set; }

    [Range(0, 1)]
    public decimal Confidence { get; set; }

    [Required]
    [MaxLength(100)]
    public string AgentName { get; set; } = string.Empty;

    public bool NeedsMoreInformation { get; set; }
 
    public IReadOnlyList<string> FollowUpQuestions { get; set; }
        = Array.Empty<string>();
}
