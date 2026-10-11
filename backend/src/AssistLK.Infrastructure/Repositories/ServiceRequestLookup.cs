using System;
using System.Threading;
using System.Threading.Tasks;
using AssistLK.Application.Interfaces;
using AssistLK.Domain.Entities;
using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.Infrastructure.Repositories;

/// <summary>
/// Read-only ServiceRequest lookup used by Component 3 to snapshot
/// location data into a Booking at approval time.
///
/// Design notes:
///   - Uses AsNoTracking() because the returned entity is only read,
///     never modified or saved by Component 3.
///   - Does not call SaveChanges. No writes are possible from this class.
///   - Lives in Infrastructure so Component 3's Application layer remains
///     free of EF Core dependencies.
/// </summary>
public class ServiceRequestLookup : IServiceRequestLookup
{
    private readonly AssistLKDbContext _context;

    public ServiceRequestLookup(AssistLKDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceRequest?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.ServiceRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(sr => sr.Id == id, cancellationToken);
    }
}