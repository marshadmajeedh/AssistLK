using System.ComponentModel.DataAnnotations;

namespace AssistLK.Application.Auth.DTOs;

public class ResendOtpRequest
{
    [Required]
    public Guid ChallengeId { get; set; }
}
