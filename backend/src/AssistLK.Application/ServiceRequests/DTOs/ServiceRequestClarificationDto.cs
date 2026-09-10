namespace AssistLK.Application.ServiceRequests.DTOs;

public class ServiceRequestClarificationDto
{
    public Guid Id { get; set; }

    public Guid ServiceRequestId { get; set; }

    public int ClarificationRound { get; set; }

    public int Sequence { get; set; }

    public string Question { get; set; } = string.Empty;

    public string? Answer { get; set; }

    public DateTime? AnsweredAt { get; set; }

    public DateTime? SupersededAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
