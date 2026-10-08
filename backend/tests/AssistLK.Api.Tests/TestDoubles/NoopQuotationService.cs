using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AssistLK.Application.Quotations;
using AssistLK.Application.Services.Quotations;

namespace AssistLK.Api.Tests.TestDoubles;

/// <summary>
/// Deterministic test double for IQuotationService used by HTTP-level
/// integration tests that verify routing, authorization and response shape.
/// Business logic and persistence are verified separately in
/// QuotationServiceTests and the PostgreSQL integration suites.
/// </summary>
public sealed class NoopQuotationService : IQuotationService
{
    public QuotationDto? CreateResult { get; set; }
    public QuotationDto? GetByIdResult { get; set; }
    public List<QuotationDto> GetByServiceRequestResult { get; set; } = new();
    public QuotationApprovalWorkflowDto? StartApprovalWorkflowResult { get; set; }
    public BookingDto? ApproveResult { get; set; }
    public QuotationDto? RejectResult { get; set; }
    public BookingDto? GetBookingByIdResult { get; set; }
    public List<BookingStatusHistoryDto> GetBookingStatusHistoryResult { get; set; } = new();

    public Func<int, string, string, string?, CancellationToken, Task>? OnResume { get; set; }

    public Task<QuotationDto> CreateAsync(CreateQuotationDto dto, string providerUserId, CancellationToken cancellationToken = default)
    {
        if (CreateResult is null) throw new InvalidOperationException("CreateResult not configured.");
        return Task.FromResult(CreateResult);
    }

    public Task<QuotationDto?> GetByIdAsync(int id)
        => Task.FromResult(GetByIdResult);

    public Task<IEnumerable<QuotationDto>> GetByServiceRequestAsync(Guid serviceRequestId)
        => Task.FromResult<IEnumerable<QuotationDto>>(GetByServiceRequestResult);

    public Task<QuotationApprovalWorkflowDto> StartApprovalWorkflowAsync(int quotationId, string providerUserId, CancellationToken cancellationToken = default)
    {
        if (StartApprovalWorkflowResult is null) throw new InvalidOperationException("StartApprovalWorkflowResult not configured.");
        return Task.FromResult(StartApprovalWorkflowResult);
    }

    public Task ResumeApprovalWorkflowAsync(int quotationId, string threadId, string decision, string? remarks, CancellationToken cancellationToken = default)
        => OnResume?.Invoke(quotationId, threadId, decision, remarks, cancellationToken) ?? Task.CompletedTask;

    public Task<BookingDto> ApproveAsync(int quotationId, ApproveQuotationDto dto, string customerUserId, CancellationToken cancellationToken = default)
    {
        if (ApproveResult is null) throw new InvalidOperationException("ApproveResult not configured.");
        return Task.FromResult(ApproveResult);
    }

    public Task<QuotationDto> RejectAsync(int quotationId, RejectQuotationDto dto, string customerUserId, CancellationToken cancellationToken = default)
    {
        if (RejectResult is null) throw new InvalidOperationException("RejectResult not configured.");
        return Task.FromResult(RejectResult);
    }

    public Task<BookingDto?> GetBookingByIdAsync(int id)
        => Task.FromResult(GetBookingByIdResult);

    public Task<IEnumerable<BookingStatusHistoryDto>> GetBookingStatusHistoryAsync(int bookingId)
        => Task.FromResult<IEnumerable<BookingStatusHistoryDto>>(GetBookingStatusHistoryResult);
}