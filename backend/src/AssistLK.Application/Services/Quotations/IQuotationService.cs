using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AssistLK.Application.Quotations;

namespace AssistLK.Application.Services.Quotations;

public interface IQuotationService
{
    // ------------------------------------------------------------------
    // Quotation CRUD
    // ------------------------------------------------------------------
    Task<QuotationDto> CreateAsync(
        CreateQuotationDto dto,
        string providerUserId,
        CancellationToken cancellationToken = default);

    Task<QuotationDto?> GetByIdAsync(int id);

    Task<IEnumerable<QuotationDto>> GetByServiceRequestAsync(Guid serviceRequestId);

    // ------------------------------------------------------------------
    // Agentic AI workflow bridge
    // ------------------------------------------------------------------
    Task<QuotationApprovalWorkflowDto> StartApprovalWorkflowAsync(
        int quotationId,
        string providerUserId,
        CancellationToken cancellationToken = default);

    Task ResumeApprovalWorkflowAsync(
        int quotationId,
        string threadId,
        string decision,
        string? remarks,
        CancellationToken cancellationToken = default);

    // ------------------------------------------------------------------
    // Decision handling (Customer)
    // ------------------------------------------------------------------
    Task<BookingDto> ApproveAsync(
        int quotationId,
        ApproveQuotationDto dto,
        string customerUserId,
        CancellationToken cancellationToken = default);

    Task<QuotationDto> RejectAsync(
        int quotationId,
        RejectQuotationDto dto,
        string customerUserId,
        CancellationToken cancellationToken = default);

    // ------------------------------------------------------------------
    // Booking read
    // ------------------------------------------------------------------
    Task<BookingDto?> GetBookingByIdAsync(int id);

    Task<IEnumerable<BookingStatusHistoryDto>> GetBookingStatusHistoryAsync(int bookingId);
}