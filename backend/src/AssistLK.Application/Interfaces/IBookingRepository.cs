using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AssistLK.Domain.Entities;

namespace AssistLK.Application.Interfaces;

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BookingStatusHistory>> GetStatusHistoryAsync(
        int bookingId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Booking booking, CancellationToken cancellationToken = default);

    Task AddStatusHistoryAsync(
        BookingStatusHistory entry,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}