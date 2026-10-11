namespace AssistLK.Domain.Entities;

public class ProviderSkill : BaseEntity
{
    public Guid ProviderId { get; set; }

    public ProviderProfile Provider { get; set; } = null!;

    public string Category { get; set; } = string.Empty;

    public string SkillName { get; set; } = string.Empty;

    public string? CertificationUrl { get; set; }

    public bool IsVerified { get; set; }
}