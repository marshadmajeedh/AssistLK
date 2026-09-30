namespace AssistLK.Application.Interfaces;

/// <summary>
/// Service responsible for safely recovering genuinely stale or crashed Analyzing service requests.
/// </summary>
public interface IStaleAnalysisRecoveryService
{
    /// <summary>
    /// Scans for stale Analyzing requests and recovers eligible requests to their safe pre-analysis status.
    /// Returns the number of recovered requests.
    /// </summary>
    Task<int> RecoverStaleAnalysesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Recovers a specific service request if it is eligible for stale recovery.
    /// Returns true if recovered, or false if not eligible or concurrently advanced.
    /// </summary>
    Task<bool> RecoverStaleRequestAsync(Guid serviceRequestId, CancellationToken cancellationToken = default);
}
