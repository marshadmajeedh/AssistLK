namespace AssistLK.Application.Auth.DTOs;

public class RegisterStartResponse
{
    public Guid ChallengeId { get; set; }

    public string MaskedPhoneNumber { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public int CooldownSeconds { get; set; } = 45;
}
