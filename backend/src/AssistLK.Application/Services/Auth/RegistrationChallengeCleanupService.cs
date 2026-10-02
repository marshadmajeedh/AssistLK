using AssistLK.Application.Auth;
using AssistLK.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AssistLK.Application.Services.Auth;

/// <summary>
/// Service that safely deletes obsolete registration challenges past their retention period.
/// Ensures active and recently expired/consumed challenges are preserved.
/// </summary>
public class RegistrationChallengeCleanupService : IRegistrationChallengeCleanupService
{
    private readonly IRegistrationChallengeRepository _challengeRepository;
    private readonly RegistrationChallengeRetentionOptions _options;
    private readonly ILogger<RegistrationChallengeCleanupService> _logger;

    public RegistrationChallengeCleanupService(
        IRegistrationChallengeRepository challengeRepository,
        RegistrationChallengeRetentionOptions? directOptions = null,
        IOptions<RegistrationChallengeRetentionOptions>? optionsWrapper = null,
        ILogger<RegistrationChallengeCleanupService>? logger = null)
    {
        _challengeRepository = challengeRepository;
        _options = directOptions ?? optionsWrapper?.Value ?? new RegistrationChallengeRetentionOptions();
        _logger = logger ?? NullLogger<RegistrationChallengeCleanupService>.Instance;
    }

    public Task<int> CleanupObsoleteChallengesAsync(
        CancellationToken cancellationToken = default)
    {
        return CleanupObsoleteChallengesAsync(cutoffUtcOverride: null, cancellationToken);
    }

    public async Task<int> CleanupObsoleteChallengesAsync(
        DateTime? cutoffUtcOverride,
        CancellationToken cancellationToken = default)
    {
        var retentionWindow = _options.GetRetentionTimeSpan();
        var cutoffUtc = cutoffUtcOverride ?? DateTime.SpecifyKind(DateTime.UtcNow - retentionWindow, DateTimeKind.Utc);

        _logger.LogInformation(
            "Starting registration challenge retention cleanup. RetentionWindow: {RetentionWindow}, CutoffUtc: {CutoffUtc:O}",
            retentionWindow,
            cutoffUtc);

        var deletedCount = await _challengeRepository.DeleteObsoleteChallengesAsync(cutoffUtc, cancellationToken);

        _logger.LogInformation(
            "Registration challenge retention cleanup completed. Safely removed {DeletedCount} obsolete record(s).",
            deletedCount);

        return deletedCount;
    }
}
