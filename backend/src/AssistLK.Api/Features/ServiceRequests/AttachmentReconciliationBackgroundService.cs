using AssistLK.Application.Attachments;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssistLK.Api.Features.ServiceRequests;

/// <summary>
/// Background worker that periodically reconciles private attachment physical files
/// against ServiceRequestAttachment database records.
/// Safely cleans up orphan files with scope isolation, conservative grace period,
/// and failure resilience so transient errors never crash the host.
/// </summary>
public class AttachmentReconciliationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AttachmentReconciliationOptions _options;
    private readonly ILogger<AttachmentReconciliationBackgroundService> _logger;

    public AttachmentReconciliationBackgroundService(
        IServiceScopeFactory scopeFactory,
        AttachmentReconciliationOptions? directOptions = null,
        IOptions<AttachmentReconciliationOptions>? optionsWrapper = null,
        ILogger<AttachmentReconciliationBackgroundService>? logger = null)
    {
        _scopeFactory = scopeFactory;
        _options = directOptions ?? optionsWrapper?.Value ?? new AttachmentReconciliationOptions();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AttachmentReconciliationBackgroundService>.Instance;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("AttachmentReconciliationBackgroundService is disabled by configuration.");
            return;
        }

        _logger.LogInformation(
            "AttachmentReconciliationBackgroundService started. InitialDelay: {Delay}s, IntervalHours: {Interval}h, MinimumFileAgeMinutes: {Age}m.",
            _options.InitialDelaySeconds,
            _options.IntervalHours,
            _options.MinimumFileAgeMinutes);

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
                _logger.LogInformation("Attachment reconciliation cycle starting.");
                using var scope = _scopeFactory.CreateScope();
                var reconciliationService = scope.ServiceProvider.GetRequiredService<AttachmentReconciliationService>();
                var result = await reconciliationService.ReconcileDetailedAsync(ct: stoppingToken);
                _logger.LogInformation(
                    "Attachment reconciliation cycle completed. Cleaned up {DeletedCount} orphan file(s), {ReferencedCount} referenced file(s) preserved, {SkippedGracePeriodCount} file(s) skipped within grace period.",
                    result.DeletedCount,
                    result.ReferencedCount,
                    result.SkippedGracePeriodCount);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in AttachmentReconciliationBackgroundService execution cycle. Maintenance worker will retry on next scheduled cycle.");
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

        _logger.LogInformation("AttachmentReconciliationBackgroundService stopped gracefully.");
    }
}
