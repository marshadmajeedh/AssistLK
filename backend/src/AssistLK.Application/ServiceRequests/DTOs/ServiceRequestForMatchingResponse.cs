using AssistLK.Domain.Enums;

namespace AssistLK.Application.ServiceRequests.DTOs;

public class ServiceRequestForMatchingResponse
{
    public Guid ServiceRequestId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string ProblemSummary { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public ServiceRequestUrgency Urgency { get; set; }
    public string LocationText { get; set; } = string.Empty;

    public LocationSource LocationSource { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public ServiceRequestStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
