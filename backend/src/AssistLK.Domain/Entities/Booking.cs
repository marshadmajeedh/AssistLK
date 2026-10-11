using System;
using System.Collections.Generic;

namespace AssistLK.Domain.Entities;

public class Booking
{
    public int Id { get; set; }
    public int QuotationId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid ProviderId { get; set; }
    public BookingStatus Status { get; set; }
    public DateTime ScheduledAt { get; set; }

    // -----------------------------------------------------------------
    // Location snapshot — captured at booking time from the linked
    // ServiceRequest. Uses decimal to match Component 1's schema and
    // avoid floating-point precision loss on coordinates.
    // -----------------------------------------------------------------
    public string? LocationText { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Quotation Quotation { get; set; } = null!;
    public List<BookingStatusHistory> StatusHistory { get; set; } = new();
}

public enum BookingStatus
{
    Confirmed = 0,
    ProviderAssigned = 1,
    ProviderOnTheWay = 2,
    ProviderArrived = 3,
    WorkStarted = 4,
    WorkCompleted = 5,
    Cancelled = 6
}