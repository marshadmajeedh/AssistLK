namespace AssistLK.Domain.Entities;

public class ProviderLocation : BaseEntity
{
    public Guid ProviderId { get; set; }

    public ProviderProfile Provider { get; set; } = null!;

    public decimal Latitude { get; set; }

    public decimal Longitude { get; set; }

    public decimal OperatingRadiusKm { get; set; } = 10m;

    public DateTime LastLocationUpdate { get; set; } = DateTime.UtcNow;
}