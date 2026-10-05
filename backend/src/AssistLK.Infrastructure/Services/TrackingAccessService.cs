using AssistLK.Application.Interfaces;
using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.Infrastructure.Services;

public sealed class TrackingAccessService : ITrackingAccessService
{
    private readonly IServiceJobsDbContext _serviceJobsDbContext;
    private readonly AssistLKDbContext _assistLkDbContext;

    public TrackingAccessService(
        IServiceJobsDbContext serviceJobsDbContext,
        AssistLKDbContext assistLkDbContext)
    {
        _serviceJobsDbContext = serviceJobsDbContext;
        _assistLkDbContext = assistLkDbContext;
    }

    public async Task<TrackingAccessResult> ValidateAsync(
        Guid jobId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var job = await _serviceJobsDbContext.ServiceJobs
            .AsNoTracking()
            .Where(item => item.Id == jobId)
            .Select(item => new
            {
                item.Status,
                item.ProviderId,
                item.ServiceRequestId
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (job == null)
        {
            return new TrackingAccessResult(false, false);
        }

        var isAssignedProvider = job.ProviderId.HasValue &&
            await _assistLkDbContext.ProviderProfiles
                .AnyAsync(
                    profile => profile.Id == job.ProviderId.Value &&
                              profile.UserId == userId,
                    cancellationToken);

        var isRequestingCustomer = job.ServiceRequestId.HasValue &&
            await _assistLkDbContext.ServiceRequests
                .AnyAsync(
                    request => request.Id == job.ServiceRequestId.Value &&
                              request.CustomerId == userId,
                    cancellationToken);

        return new TrackingAccessResult(
            isAssignedProvider || isRequestingCustomer,
            job.Status == AssistLK.Domain.Entities.ServiceJobStatus.OnTheWay);
    }
}