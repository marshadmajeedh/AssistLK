using System.ComponentModel.DataAnnotations;

namespace AssistLK.Application.ServiceRequests.DTOs;

public class SubmitClarificationAnswersRequest
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Clarification round must be at least 1.")]
    public int ClarificationRound { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "At least one answer must be submitted.")]
    public List<ClarificationAnswerSubmissionItem> Answers { get; set; } = new();
}

public class ClarificationAnswerSubmissionItem
{
    [Required]
    public Guid ClarificationId { get; set; }

    [Required]
    [StringLength(1000, MinimumLength = 1, ErrorMessage = "Answer must be between 1 and 1000 characters.")]
    public string Answer { get; set; } = string.Empty;
}
