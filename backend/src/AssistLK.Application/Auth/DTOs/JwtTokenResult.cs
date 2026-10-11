namespace AssistLK.Application.Auth.DTOs;

public class JwtTokenResult
{
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }
}