using System;

namespace AssistLK.Application.Quotations;

public record BookingStatusHistoryDto(
    int Id,
    int BookingId,
    string PreviousStatus,
    string NewStatus,
    string ChangedByUserId,
    string? Reason,
    DateTime ChangedAt);
