namespace AssistLK.Api.DTOs.Providers;

public sealed class ProviderProfileDto
{
    public Guid ProviderId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string BusinessName { get; init; } = string.Empty;
    public string VerificationStatus { get; init; } = string.Empty;
    public decimal AverageRating { get; init; }
    public decimal Rating => AverageRating;
    public int TotalReviews { get; init; }
    public int TotalCompletedJobs { get; init; }
    public bool IsOnline { get; init; }
    public string Category { get; init; } = string.Empty;
    public string SkillName { get; init; } = string.Empty;
    public decimal OperatingRadiusKm { get; init; }
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
    public IReadOnlyList<ProviderSkillDto> Skills { get; init; } = [];
}

public sealed class ProviderSkillDto
{
    public Guid Id { get; init; }
    public string Category { get; init; } = string.Empty;
    public string SkillName { get; init; } = string.Empty;
    public bool IsVerified { get; init; }
    public string? CertificationUrl { get; init; }
}