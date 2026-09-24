namespace AssistLK.Domain.Entities;

public class ProviderAvailability : BaseEntity
{
    public Guid ProviderId { get; set; }

    public ProviderProfile Provider { get; set; } = null!;

    public int DayOfWeek { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public bool IsAvailable { get; set; } = true;
}