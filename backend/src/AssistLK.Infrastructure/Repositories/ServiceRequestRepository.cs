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

    public async Task<ServiceRequest?> ReloadForRecoveryAsync(Guid serviceRequestId, CancellationToken cancellationToken = default)
    {
        var tracked = _context.ServiceRequests.Local.SingleOrDefault(r => r.Id == serviceRequestId);
        if (tracked is null) return await GetByIdAsync(serviceRequestId, cancellationToken: cancellationToken);
        await _context.Entry(tracked).ReloadAsync(cancellationToken);
        return _context.Entry(tracked).State == EntityState.Detached ? null : tracked;
    }

    public Task<ServiceRequest?> GetByIdAsync(
        Guid serviceRequestId,
        bool includeProblemAnalyses = false,
        CancellationToken cancellationToken = default)
    {
        return GetByIdAsync(serviceRequestId, includeProblemAnalyses, false, cancellationToken);
    }

    public async Task<ServiceRequest?> GetByIdAsync(
        Guid serviceRequestId,
        bool includeProblemAnalyses,
        bool includeClarifications,
        CancellationToken cancellationToken = default)
    {
        return await BuildQuery(includeProblemAnalyses, includeClarifications)
            .FirstOrDefaultAsync(x => x.Id == serviceRequestId, cancellationToken);
    }

    public Task<ServiceRequest?> GetByIdAndCustomerIdAsync(
        Guid serviceRequestId,
        Guid customerId,
        bool includeProblemAnalyses = false,
        CancellationToken cancellationToken = default)
    {
        return GetByIdAndCustomerIdAsync(serviceRequestId, customerId, includeProblemAnalyses, false, cancellationToken);
    }

    public async Task<ServiceRequest?> GetByIdAndCustomerIdAsync(
        Guid serviceRequestId,
        Guid customerId,
        bool includeProblemAnalyses,
        bool includeClarifications,
        CancellationToken cancellationToken = default)
    {
        return await BuildQuery(includeProblemAnalyses, includeClarifications)
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

    public async Task<IReadOnlyList<ServiceRequest>> GetAllForAdminAsync(
        ServiceRequestStatus? status = null,
        string? category = null,
        ServiceRequestUrgency? urgency = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ServiceRequests
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.ProblemAnalyses)
            .Include(x => x.Clarifications)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(x => x.Category == category);
        }

        if (urgency.HasValue)
        {
            query = query.Where(x => x.Urgency == urgency.Value);
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        ServiceRequest serviceRequest,
        CancellationToken cancellationToken = default)
    {
        await _context.ServiceRequests.AddAsync(serviceRequest, cancellationToken);
    }

    public async Task AddClarificationsAsync(
        IEnumerable<ServiceRequestClarification> clarifications,
        CancellationToken cancellationToken = default)
    {
        await _context.ServiceRequestClarifications.AddRangeAsync(clarifications, cancellationToken);
    }

    public void Update(ServiceRequest serviceRequest)
    {
        var entry = _context.Entry(serviceRequest);
        if (entry.State == EntityState.Detached)
        {
            _context.ServiceRequests.Attach(serviceRequest);
            entry.State = EntityState.Modified;
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<ServiceRequest> BuildQuery(bool includeProblemAnalyses, bool includeClarifications)
    {
        var query = _context.ServiceRequests.Include(x => x.Attachments).AsQueryable();

        if (includeProblemAnalyses)
        {
            query = query.Include(x => x.ProblemAnalyses);
        }

        if (includeClarifications)
        {
            query = query.Include(x => x.Clarifications);
        }

        return query;
    }
}
