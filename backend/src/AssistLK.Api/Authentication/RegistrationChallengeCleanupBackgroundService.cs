using AssistLK.Application.Auth;
using AssistLK.Application.Services.Auth;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AssistLK.Api.Authentication;

/// <summary>
/// Background worker that periodically cleans up obsolete registration challenges.
/// Retains active and recently expired/consumed challenges according to retention policy.
/// Safely scopes dependencies and isolates exceptions so the host process never crashes.
/// </summary>
public class RegistrationChallengeCleanupBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RegistrationChallengeRetentionOptions _options;
    private readonly ILogger<RegistrationChallengeCleanupBackgroundService> _logger;

    public RegistrationChallengeCleanupBackgroundService(
        IServiceScopeFactory scopeFactory,
        RegistrationChallengeRetentionOptions? directOptions = null,
        IOptions<RegistrationChallengeRetentionOptions>? optionsWrapper = null,
        ILogger<RegistrationChallengeCleanupBackgroundService>? logger = null)
    {
        _scopeFactory = scopeFactory;
        _options = directOptions ?? optionsWrapper?.Value ?? new RegistrationChallengeRetentionOptions();
        _logger = logger ?? NullLogger<RegistrationChallengeCleanupBackgroundService>.Instance;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("RegistrationChallengeCleanupBackgroundService is disabled by configuration.");
            return;
        }

        _logger.LogInformation(
            "RegistrationChallengeCleanupBackgroundService started. InitialDelay: {Delay}s, IntervalHours: {Interval}h, RetentionHours: {Retention}h.",
            _options.InitialDelaySeconds,
            _options.IntervalHours,
            _options.RetentionHours);

        if (_options.InitialDelaySeconds > 0)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.InitialDelaySeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Registration challenge cleanup cycle starting.");
                using var scope = _scopeFactory.CreateScope();
                var cleanupService = scope.ServiceProvider.GetRequiredService<IRegistrationChallengeCleanupService>();
                var deletedCount = await cleanupService.CleanupObsoleteChallengesAsync(stoppingToken);
                _logger.LogInformation(
                    "Registration challenge cleanup cycle completed. Safely deleted {DeletedCount} obsolete registration challenge(s).",
                    deletedCount);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in RegistrationChallengeCleanupBackgroundService execution cycle. Maintenance worker will retry on next scheduled cycle.");
            }

            try
            {
                var delay = _options.IntervalSeconds.HasValue && _options.IntervalSeconds.Value > 0
                    ? TimeSpan.FromSeconds(_options.IntervalSeconds.Value)
                    : TimeSpan.FromHours(_options.IntervalHours);
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("RegistrationChallengeCleanupBackgroundService stopped gracefully.");
    }
}
