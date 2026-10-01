namespace AssistLK.Application.Interfaces;

using AssistLK.Application.ServiceRequests.DTOs;

public sealed class ServiceRequestActivityData
{
    public Guid ServiceRequestId { get; init; }
    public Guid? ServiceJobId { get; init; }
    public CompletionRecordResponse? CompletionRecord { get; init; }
    public FeedbackSummaryResponse? Feedback { get; init; }
}

public interface IServiceJobRepository
{
    Task<IReadOnlyDictionary<Guid, ServiceRequestActivityData>>
        GetActivityByServiceRequestIdsAsync(
            IEnumerable<Guid> serviceRequestIds,
            CancellationToken cancellationToken = default);

    Task<Guid?> GetIdByServiceRequestIdAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default);

    Task<CompletionRecordResponse?> GetCompletionRecordByServiceRequestIdAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default);

    Task<FeedbackSummaryResponse?> GetFeedbackByServiceRequestIdAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default);
}