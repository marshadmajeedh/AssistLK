using AssistLK.Domain.Enums;

namespace AssistLK.Domain.Entities;

public class User : BaseEntity
{
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public UserRole Role { get; set; }

    public bool IsActive { get; set; } = true;
}