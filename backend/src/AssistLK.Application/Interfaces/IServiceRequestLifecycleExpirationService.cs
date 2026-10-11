namespace AssistLK.Application.Interfaces;

/// <summary>
/// Service responsible for identifying and safely timing out stale AwaitingInformation service requests
/// whose actionable clarification questions have remained unanswered beyond the configured threshold.
/// Does NOT mutate ReadyForMatching requests.
/// </summary>
public interface IServiceRequestLifecycleExpirationService
{
    /// <summary>
    /// Scans for and safely cancels stale AwaitingInformation service requests whose clarification window has expired.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of expired and cancelled service requests.</returns>
    Task<int> ExpireStaleAwaitingInformationRequestsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates and cancels a specific service request if it meets the stale AwaitingInformation criteria.
    /// </summary>
    /// <param name="serviceRequestId">The service request ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the request was safely cancelled; otherwise false.</returns>
    Task<bool> ExpireStaleAwaitingInformationRequestAsync(Guid serviceRequestId, CancellationToken cancellationToken = default);
}
