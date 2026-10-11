using AssistLK.Application.Interfaces;
using AssistLK.Application.ServiceRequests;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssistLK.Api.Features.ServiceRequests;

/// <summary>
/// Background worker that periodically checks for and safely cancels stale AwaitingInformation
/// ServiceRequests whose actionable clarification timeout has expired.
/// Does NOT mutate ReadyForMatching requests.
/// </summary>
public class ServiceRequestLifecycleExpirationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ServiceRequestLifecycleOptions _options;
    private readonly ILogger<ServiceRequestLifecycleExpirationBackgroundService> _logger;

    public ServiceRequestLifecycleExpirationBackgroundService(
        IServiceScopeFactory scopeFactory,
        ServiceRequestLifecycleOptions? directOptions = null,
        IOptions<ServiceRequestLifecycleOptions>? optionsWrapper = null,
        ILogger<ServiceRequestLifecycleExpirationBackgroundService>? logger = null)
    {
        _scopeFactory = scopeFactory;
        _options = directOptions ?? optionsWrapper?.Value ?? new ServiceRequestLifecycleOptions();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ServiceRequestLifecycleExpirationBackgroundService>.Instance;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("ServiceRequestLifecycleExpirationBackgroundService is disabled by configuration.");
            return;
        }

        _logger.LogInformation(
            "ServiceRequestLifecycleExpirationBackgroundService started. AwaitingInfoTimeout: {TimeoutHours}h, PollInterval: {Interval} min, InitialDelay: {Delay} sec.",
            _options.AwaitingInformationTimeoutHours,
            _options.PollIntervalMinutes,
            _options.InitialDelaySeconds);

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
                using var scope = _scopeFactory.CreateScope();
                var expirationService = scope.ServiceProvider.GetRequiredService<IServiceRequestLifecycleExpirationService>();
                var expiredCount = await expirationService.ExpireStaleAwaitingInformationRequestsAsync(stoppingToken);

                if (expiredCount > 0)
                {
                    _logger.LogInformation(
                        "ServiceRequestLifecycleExpiration cycle completed. Successfully cancelled {ExpiredCount} timed-out request(s).",
                        expiredCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error in ServiceRequestLifecycleExpirationBackgroundService execution cycle.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(_options.PollIntervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("ServiceRequestLifecycleExpirationBackgroundService stopped gracefully.");
    }
}
