using System.ComponentModel.DataAnnotations;

namespace AssistLK.Application.Auth.DTOs;

public class VerifyOtpRequest
{
    [Required]
    public Guid ChallengeId { get; set; }

    [Required]
    [RegularExpression(@"^[0-9]{6}$", ErrorMessage = "Verification code must be exactly 6 numeric digits.")]
    public string Otp { get; set; } = string.Empty;
}
