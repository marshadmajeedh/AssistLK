using System;

namespace AssistLK.Domain.Entities;

public class BookingStatusHistory
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public BookingStatus PreviousStatus { get; set; }
    public BookingStatus NewStatus { get; set; }
    public Guid ChangedByUserId { get; set; }          // ✅ Guid
    public string? Reason { get; set; }
    public DateTime ChangedAt { get; set; }

    public Booking Booking { get; set; } = null!;
}