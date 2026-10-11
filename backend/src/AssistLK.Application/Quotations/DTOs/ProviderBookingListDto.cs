using System;

namespace AssistLK.Application.DTOs;

public class ProviderBookingListDto
{
    public Guid Id { get; set; }
    public Guid? ServiceRequestId { get; set; }
    public Guid ProviderId { get; set; }
    public int? BookingId { get; set; }

    public string CustomerName { get; set; } = "Customer";
    public string ServiceTitle { get; set; } = string.Empty;
    public string LocationText { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    /// <summary>
    /// Human-readable status, e.g. "CONFIRMED", "ASSIGNED", "ON THE WAY",
    /// "IN PROGRESS", "COMPLETED", "PENDING QUOTE".
    /// </summary>
    public string StatusLabel { get; set; } = "UNKNOWN";

    /// <summary>
    /// Integer status code used by the Flutter side to map to BookingStatus.
    /// Mirrors BookingStatus enum from the domain.
    /// </summary>
    public int StatusCode { get; set; }

    public DateTime? ScheduledAt { get; set; }

    public bool IsCompleted { get; set; }
    public bool IsPendingQuote { get; set; }
}