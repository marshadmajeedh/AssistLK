using AssistLK.Domain.Enums;

namespace AssistLK.Application.ServiceRequests.DTOs;

public class ServiceRequestResponse
{
    [System.Text.Json.Serialization.JsonIgnore]
    public long EvidenceRevision { get; set; } = 1;

    public Guid ServiceRequestId { get; set; }
    public Guid? ServiceJobId { get; set; }
    public Guid CustomerId { get; set; }
    public string? CategoryHint { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string LocationText { get; set; } = string.Empty;

    public LocationSource LocationSource { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public ServiceRequestUrgency Urgency { get; set; }
    public ServiceRequestStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ProblemAnalysisSummaryDto? LatestAnalysis { get; set; }

    public IReadOnlyList<ServiceRequestClarificationDto> Clarifications { get; set; }
        = Array.Empty<ServiceRequestClarificationDto>();
}
