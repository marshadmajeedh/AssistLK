using AssistLK.Application.Interfaces;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.Infrastructure.Repositories;

public class ServiceRequestRepository : IServiceRequestRepository
{
    private readonly AssistLKDbContext _context;

    public ServiceRequestRepository(AssistLKDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceRequest?> GetByIdAsync(
        Guid serviceRequestId,
        bool includeProblemAnalyses = false,
        CancellationToken cancellationToken = default)
    {
        return await BuildQuery(includeProblemAnalyses)
            .FirstOrDefaultAsync(x => x.Id == serviceRequestId, cancellationToken);
    }

    public async Task<ServiceRequest?> GetByIdAndCustomerIdAsync(
        Guid serviceRequestId,
        Guid customerId,
        bool includeProblemAnalyses = false,
        CancellationToken cancellationToken = default)
    {
        return await BuildQuery(includeProblemAnalyses)
            .FirstOrDefaultAsync(
                x => x.Id == serviceRequestId && x.CustomerId == customerId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<ServiceRequest>> GetByCustomerIdAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ServiceRequests
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ServiceRequest>> GetByStatusAsync(
        ServiceRequestStatus status,
        CancellationToken cancellationToken = default)
    {
        return await _context.ServiceRequests
            .Where(x => x.Status == status)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        ServiceRequest serviceRequest,
        CancellationToken cancellationToken = default)
    {
        await _context.ServiceRequests.AddAsync(serviceRequest, cancellationToken);
    }

    public void Update(ServiceRequest serviceRequest)
    {
        _context.ServiceRequests.Update(serviceRequest);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<ServiceRequest> BuildQuery(bool includeProblemAnalyses)
    {
        var query = _context.ServiceRequests.AsQueryable();
        return includeProblemAnalyses
            ? query.Include(x => x.ProblemAnalyses)
            : query;
    }
}
