using AssistLK.Application.Interfaces;
using AssistLK.Application.ServiceRequests.DTOs;

namespace AssistLK.IntegrationTests.TestDoubles;

public sealed class InMemoryServiceJobRepository : IServiceJobRepository
{
    public Task<Guid?> GetIdByServiceRequestIdAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Guid?>(null);
    }

    public Task<IReadOnlyDictionary<Guid, ServiceRequestActivityData>>
        GetActivityByServiceRequestIdsAsync(
            IEnumerable<Guid> serviceRequestIds,
            CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyDictionary<Guid, ServiceRequestActivityData>>(
            new Dictionary<Guid, ServiceRequestActivityData>());
    }

    public Task<CompletionRecordResponse?> GetCompletionRecordByServiceRequestIdAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<CompletionRecordResponse?>(null);
    }

    public Task<FeedbackSummaryResponse?> GetFeedbackByServiceRequestIdAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<FeedbackSummaryResponse?>(null);
    }
}