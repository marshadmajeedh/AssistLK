namespace AssistLK.Domain.Entities;

public class ServiceRequestClarification : BaseEntity
{
    public Guid ServiceRequestId { get; set; }

    public ServiceRequest ServiceRequest { get; set; } = null!;

    public int ClarificationRound { get; set; }

    public int Sequence { get; set; }

    public string Question { get; set; } = string.Empty;

    public string? Answer { get; set; }

    public DateTime? AnsweredAt { get; set; }

    public DateTime? SupersededAt { get; set; }
}
