using System;

namespace AssistLK.Application.Quotations;

public record BookingDto(
    int Id,
    int QuotationId,
    Guid CustomerId,
    Guid ProviderId,
    string Status,
    DateTime ScheduledAt,
    string? LocationText,
    decimal? Latitude,
    decimal? Longitude,
    DateTime CreatedAt,
    DateTime UpdatedAt);