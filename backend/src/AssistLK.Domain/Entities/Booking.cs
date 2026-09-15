namespace AssistLK.Domain.Entities;

public class Booking
{
    public int Id { get; set; }
    public int QuotationId { get; set; }
    public int CustomerId { get; set; }
    public int ProviderId { get; set; }
    public BookingStatus Status { get; set; }
    public DateTime ScheduledAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Quotation Quotation { get; set; } = null!;
    public List<BookingStatusHistory> StatusHistory { get; set; } = new();
}

public enum BookingStatus
{
    Confirmed,
    ProviderAssigned,
    ProviderOnTheWay,
    ProviderArrived,
    WorkStarted,
    WorkCompleted,
    Cancelled
}