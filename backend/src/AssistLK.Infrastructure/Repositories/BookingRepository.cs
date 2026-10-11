using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssistLK.Application.Interfaces;
using AssistLK.Domain.Entities;
using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.Infrastructure.Repositories;

public class BookingRepository : IBookingRepository
{
    private readonly AssistLKDbContext _context;

    public BookingRepository(AssistLKDbContext context)
    {
        _context = context;
    }

    public async Task<Booking?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Bookings
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<BookingStatusHistory>> GetStatusHistoryAsync(
        int bookingId,
        CancellationToken cancellationToken = default)
    {
        return await _context.BookingStatusHistories
            .Where(h => h.BookingId == bookingId)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        Booking booking,
        CancellationToken cancellationToken = default)
    {
        await _context.Bookings.AddAsync(booking, cancellationToken);
    }

    public async Task AddStatusHistoryAsync(
        BookingStatusHistory entry,
        CancellationToken cancellationToken = default)
    {
        await _context.BookingStatusHistories.AddAsync(entry, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}