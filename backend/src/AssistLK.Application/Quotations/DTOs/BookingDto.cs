using System;

namespace AssistLK.Application.Quotations;

/// <summary>
/// Data Transfer Object representing a confirmed booking.
/// Created when a customer approves a quotation (Component 3 business rule).
/// Consumed by Component 4 (Service Tracking) to manage the provider's journey.
/// </summary>
public record BookingDto(
    int Id,
    int QuotationId,
    int CustomerId,
    int ProviderId,
    string Status,
    DateTime ScheduledAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);