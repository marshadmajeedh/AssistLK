using System.Collections.Generic;
using System.Threading.Tasks;
using AssistLK.Application.Quotations;

namespace AssistLK.Application.Services.Quotations;

public interface IQuotationService
{
    Task<QuotationDto> CreateAsync(CreateQuotationDto dto, string providerUserId);
    Task<QuotationDto?> GetByIdAsync(int id);
    Task<QuotationDto> SendForApprovalAsync(int quotationId, string providerUserId);
    Task<BookingDto> ApproveAsync(int quotationId, ApproveQuotationDto dto, string customerUserId);
    Task<QuotationDto> RejectAsync(int quotationId, RejectQuotationDto dto, string customerUserId);
    Task<IEnumerable<QuotationDto>> GetByServiceRequestAsync(int serviceRequestId);
    Task<BookingDto?> GetBookingByIdAsync(int id);
    Task<IEnumerable<BookingStatusHistoryDto>> GetBookingStatusHistoryAsync(int bookingId);
}