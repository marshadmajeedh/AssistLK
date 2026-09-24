using AssistLK.Domain.Enums;

namespace AssistLK.Domain.Entities;

public class ProviderProfile : BaseEntity
{
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public string BusinessName { get; set; } = string.Empty;

    public ProviderVerificationStatus VerificationStatus { get; set; } = ProviderVerificationStatus.Pending;

    public decimal Rating { get; set; } = 0m;

    public int TotalCompletedJobs { get; set; }

    public int MaxActiveJobs { get; set; } = 3;

    public bool IsOnline { get; set; }

    public ICollection<ProviderSkill> Skills { get; set; } = new List<ProviderSkill>();

    public ICollection<ProviderLocation> Locations { get; set; } = new List<ProviderLocation>();

    public ICollection<ProviderAvailability> Availability { get; set; } = new List<ProviderAvailability>();
}