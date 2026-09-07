using AssistLK.Domain.Enums;

namespace AssistLK.Application.ServiceRequests.DTOs;

public class ServiceRequestResponse
{
    public Guid ServiceRequestId { get; set; }
    public Guid CustomerId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string LocationText { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public ServiceRequestUrgency Urgency { get; set; }
    public ServiceRequestStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
