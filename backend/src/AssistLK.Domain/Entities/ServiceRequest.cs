using AssistLK.Domain.Enums;

namespace AssistLK.Domain.Entities;

public class ServiceRequest : BaseEntity
{
    public long EvidenceRevision { get; set; } = 1;

    public Guid CustomerId { get; set; }

    public User Customer { get; set; } = null!;

    public string? CategoryHint { get; set; }

    public string Category { get; set; } = "Unclassified";

    public string Description { get; set; } = string.Empty;

    public string LocationText { get; set; } = string.Empty;

    public LocationSource LocationSource { get; set; } = LocationSource.Manual;

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public ServiceRequestUrgency Urgency { get; set; }

    public ServiceRequestStatus Status { get; set; } = ServiceRequestStatus.Created;

    public ICollection<ServiceRequestAttachment> Attachments { get; set; } = new List<ServiceRequestAttachment>();

    public ICollection<ProblemAnalysis> ProblemAnalyses { get; set; }
        = new List<ProblemAnalysis>();

    public ICollection<ServiceRequestClarification> Clarifications { get; set; }
        = new List<ServiceRequestClarification>();
}
