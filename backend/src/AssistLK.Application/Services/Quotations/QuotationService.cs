using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AssistLK.Application.Quotations;

namespace AssistLK.Application.Services.Quotations;

public class QuotationService : IQuotationService
{
    public Task<QuotationDto> CreateAsync(CreateQuotationDto dto, string providerUserId)
        => throw new NotImplementedException();

    public Task<QuotationDto?> GetByIdAsync(int id)
        => throw new NotImplementedException();

    public Task<QuotationDto> SendForApprovalAsync(int quotationId, string providerUserId)
        => throw new NotImplementedException();

    public Task<BookingDto> ApproveAsync(int quotationId, ApproveQuotationDto dto, string customerUserId)
        => throw new NotImplementedException();

    public Task<QuotationDto> RejectAsync(int quotationId, RejectQuotationDto dto, string customerUserId)
        => throw new NotImplementedException();

    public Task<IEnumerable<QuotationDto>> GetByServiceRequestAsync(int serviceRequestId)
        => throw new NotImplementedException();

    public Task<BookingDto?> GetBookingByIdAsync(int id)
        => throw new NotImplementedException();

    public Task<IEnumerable<BookingStatusHistoryDto>> GetBookingStatusHistoryAsync(int bookingId)
        => throw new NotImplementedException();
}