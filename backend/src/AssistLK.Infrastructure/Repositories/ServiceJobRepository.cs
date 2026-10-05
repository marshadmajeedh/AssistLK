using AssistLK.Application.Interfaces;
using AssistLK.Application.ServiceRequests.DTOs;
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
                    }
            })
            .ToListAsync(cancellationToken);

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