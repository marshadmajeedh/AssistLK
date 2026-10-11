namespace AssistLK.Application.Services.Auth;

/// <summary>
/// Service contract for cleaning up obsolete registration challenges.
/// </summary>
public interface IRegistrationChallengeCleanupService
{
    /// <summary>
    /// Deletes challenges that are expired or consumed older than the configured retention threshold.
    /// </summary>
    Task<int> CleanupObsoleteChallengesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes challenges older than an explicitly provided cutoff timestamp.
    /// </summary>
    Task<int> CleanupObsoleteChallengesAsync(
        DateTime? cutoffUtcOverride,
        CancellationToken cancellationToken = default);
}
