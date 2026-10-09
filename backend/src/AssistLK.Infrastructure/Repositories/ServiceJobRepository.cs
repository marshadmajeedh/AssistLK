using AssistLK.Application.Interfaces;
using AssistLK.Application.ServiceRequests.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.Infrastructure.Repositories;

public class ServiceJobRepository : IServiceJobRepository
{
    private readonly IServiceJobsDbContext _context;

    public ServiceJobRepository(IServiceJobsDbContext context)
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

    public async Task<IReadOnlyDictionary<Guid, ServiceRequestActivityData>>
        GetActivityByServiceRequestIdsAsync(
            IEnumerable<Guid> serviceRequestIds,
            CancellationToken cancellationToken = default)
    {
        var ids = serviceRequestIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, ServiceRequestActivityData>();
        }

        var activity = await _context.ServiceJobs
            .AsNoTracking()
            .Where(job => job.ServiceRequestId.HasValue &&
                          ids.Contains(job.ServiceRequestId.Value))
            .Select(job => new ServiceRequestActivityData
            {
                ServiceRequestId = job.ServiceRequestId!.Value,
                ServiceJobId = job.Id,
                JobStatus = job.Status,
                CompletionRecord = job.CompletionRecord == null
                    ? null
                    : new CompletionRecordResponse
                    {
                        ProofOfWorkImageUrl = job.CompletionRecord.ProofOfWorkImageUrl,
                        SummaryNotes = job.CompletionRecord.WorkSummary
                    },
                Feedback = job.Feedback == null
                    ? null
                    : new FeedbackSummaryResponse
                    {
                        Rating = job.Feedback.Rating,
                        Comment = job.Feedback.Comment
                    },
                ProviderId = job.ProviderId
            })
            .ToListAsync(cancellationToken);

        if (_context is AssistLK.Infrastructure.Data.AssistLKDbContext dbContext)
        {
            var providerIds = activity
                .Where(a => a.ProviderId.HasValue)
                .Select(a => a.ProviderId!.Value)
                .Distinct()
                .ToList();

            if (providerIds.Count > 0)
            {
                var profiles = await dbContext.ProviderProfiles
                    .AsNoTracking()
                    .Where(p => providerIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => p.BusinessName, cancellationToken);

                foreach (var item in activity)
                {
                    if (item.ProviderId.HasValue && profiles.TryGetValue(item.ProviderId.Value, out var name))
                    {
                        item.ProviderBusinessName = name;
                    }
                }
            }
        }

        return activity.ToDictionary(item => item.ServiceRequestId);
    }

    public Task<CompletionRecordResponse?> GetCompletionRecordByServiceRequestIdAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default)
    {
        return _context.ServiceJobs
            .AsNoTracking()
            .Where(job => job.ServiceRequestId == serviceRequestId)
            .Select(job => job.CompletionRecord == null
                ? null
                : new CompletionRecordResponse
                {
                    ProofOfWorkImageUrl = job.CompletionRecord.ProofOfWorkImageUrl,
                    SummaryNotes = job.CompletionRecord.WorkSummary
                })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<FeedbackSummaryResponse?> GetFeedbackByServiceRequestIdAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default)
    {
        return _context.ServiceJobs
            .AsNoTracking()
            .Where(job => job.ServiceRequestId == serviceRequestId)
            .Select(job => job.Feedback == null
                ? null
                : new FeedbackSummaryResponse
                {
                    Rating = job.Feedback.Rating,
                    Comment = job.Feedback.Comment
                })
            .FirstOrDefaultAsync(cancellationToken);
    }
}