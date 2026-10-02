using AssistLK.Domain.Enums;

namespace AssistLK.Domain.Entities;

public class RegistrationChallenge : BaseEntity
{
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Customer;

    public string OtpHash { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public int AttemptCount { get; set; } = 0;

    public int MaxAttempts { get; set; } = 5;

    public int ResendCount { get; set; } = 0;

    public DateTime LastSentAtUtc { get; set; }

    public bool IsConsumed { get; set; } = false;
}
