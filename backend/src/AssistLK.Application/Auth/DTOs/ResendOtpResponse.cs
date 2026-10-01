namespace AssistLK.Application.Auth.DTOs;

public class ResendOtpResponse
{
    public int CooldownSeconds { get; set; } = 45;

    public DateTime ExpiresAtUtc { get; set; }
}
