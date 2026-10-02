using AssistLK.Application.Interfaces;
using AssistLK.Application.ServiceRequests;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssistLK.Api.Features.ServiceRequests;

/// <summary>
/// Background worker that periodically runs stale analysis recovery for C1 ServiceRequests.
/// Uses scoped dependencies correctly via IServiceScopeFactory and supports graceful shutdown.
/// </summary>
public class StaleAnalysisRecoveryBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly C1RecoveryOptions _options;
    private readonly ILogger<StaleAnalysisRecoveryBackgroundService> _logger;

    public StaleAnalysisRecoveryBackgroundService(
        IServiceScopeFactory scopeFactory,
        C1RecoveryOptions? directOptions = null,
        IOptions<C1RecoveryOptions>? optionsWrapper = null,
        ILogger<StaleAnalysisRecoveryBackgroundService>? logger = null)
    {
        _scopeFactory = scopeFactory;
        _options = directOptions ?? optionsWrapper?.Value ?? new C1RecoveryOptions();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<StaleAnalysisRecoveryBackgroundService>.Instance;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("StaleAnalysisRecoveryBackgroundService is disabled by configuration.");
            return;
        }

        _logger.LogInformation(
            "StaleAnalysisRecoveryBackgroundService started. StaleThreshold: {StaleMinutes} min, PollInterval: {Interval} sec, InitialDelay: {Delay} sec.",
            _options.StaleAnalysisMinutes,
            _options.PollIntervalSeconds,
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
                var recoveryService = scope.ServiceProvider.GetRequiredService<IStaleAnalysisRecoveryService>();
                var recoveredCount = await recoveryService.RecoverStaleAnalysesAsync(stoppingToken);
                if (recoveredCount > 0)
                {
                    _logger.LogInformation(
                        "StaleAnalysisRecovery cycle completed. Successfully recovered {RecoveredCount} stale request(s).",
                        recoveredCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in StaleAnalysisRecoveryBackgroundService execution cycle.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("StaleAnalysisRecoveryBackgroundService stopped gracefully.");
    }
}
