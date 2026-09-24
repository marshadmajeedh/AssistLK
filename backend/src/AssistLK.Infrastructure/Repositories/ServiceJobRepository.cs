using AssistLK.Application.Interfaces;
using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.Infrastructure.Repositories;

public class ServiceJobRepository : IServiceJobRepository
{
    private readonly ApplicationDbContext _context;

    public ServiceJobRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<Guid?> GetIdByServiceRequestIdAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default)
    {
        return _context.ServiceJobs
            .AsNoTracking()
            .Where(job => job.ServiceRequestId == serviceRequestId)
            .Select(job => (Guid?)job.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}