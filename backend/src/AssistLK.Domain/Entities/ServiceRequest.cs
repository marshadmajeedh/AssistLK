using AssistLK.Domain.Enums;

namespace AssistLK.Domain.Entities;

public class ServiceRequest : BaseEntity
{
    public Guid CustomerId { get; set; }

    public User Customer { get; set; } = null!;

    public string Category { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string LocationText { get; set; } = string.Empty;

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public ServiceRequestUrgency Urgency { get; set; }

    public ServiceRequestStatus Status { get; set; }

    public ICollection<ProblemAnalysis> ProblemAnalyses { get; set; }
        = new List<ProblemAnalysis>();
}