using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssistLK.Application.Interfaces;
using AssistLK.Application.Services.Providers;
using AssistLK.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AssistLK.Api.Features.Providers;

public class ProviderMatchingBackgroundWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProviderMatchingBackgroundWorker> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

    public ProviderMatchingBackgroundWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ProviderMatchingBackgroundWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ProviderMatchingBackgroundWorker started. Polling interval: {Interval} seconds.", _pollInterval.TotalSeconds);

        // Initial brief delay to allow API startup
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        using var timer = new PeriodicTimer(_pollInterval);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessReadyRequestsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in ProviderMatchingBackgroundWorker loop.");
            }
        }
    }

    private async Task ProcessReadyRequestsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var serviceRequestRepository = scope.ServiceProvider.GetRequiredService<IServiceRequestRepository>();
        var matchingCoordinator = scope.ServiceProvider.GetRequiredService<IProviderMatchingCoordinator>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IAgentWorkflowDbContext>();

        var readyRequests = await serviceRequestRepository.GetByStatusAsync(
            ServiceRequestStatus.ReadyForMatching,
            stoppingToken);

        if (readyRequests == null || readyRequests.Count == 0)
        {
            return;
        }

        var readyIds = readyRequests.Select(r => r.Id).ToList();

        // 1. Exclude requests that already have an active/pending/completed execution
        var activeOrCompletedSrIds = await dbContext.MatchingExecutions
            .Where(e => readyIds.Contains(e.ServiceRequestId) &&
                       (e.Status == MatchingExecutionStatus.Running
                     || e.Status == MatchingExecutionStatus.PendingApproval
                     || e.Status == MatchingExecutionStatus.Completed))
            .Select(e => e.ServiceRequestId)
            .Distinct()
            .ToListAsync(stoppingToken);

        // 2. Exclude requests that recently failed (within the last 2 minutes) to avoid spamming the AI engine
        var recentlyFailedSrIds = await dbContext.MatchingExecutions
            .Where(e => readyIds.Contains(e.ServiceRequestId) &&
                       e.Status == MatchingExecutionStatus.Failed &&
                       e.CompletedAt != null &&
                       e.CompletedAt > DateTime.UtcNow.AddMinutes(-2))
            .Select(e => e.ServiceRequestId)
            .Distinct()
            .ToListAsync(stoppingToken);

        var excludedIds = activeOrCompletedSrIds.Concat(recentlyFailedSrIds).ToHashSet();
        var toMatchIds = readyIds.Where(id => !excludedIds.Contains(id)).ToList();

        if (toMatchIds.Count == 0)
        {
            return;
        }

        _logger.LogInformation("ProviderMatchingBackgroundWorker found {Count} ready request(s) to match.", toMatchIds.Count);

        foreach (var id in toMatchIds)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                var result = await matchingCoordinator.ExecuteMatchForRequestAsync(id, stoppingToken);
                _logger.LogInformation("Auto-dispatched match for ServiceRequest {Id}: Status={Status}, ThreadId={ThreadId}", id, result.Status, result.ThreadId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-dispatch matching for ServiceRequest {ServiceRequestId}", id);
            }
        }
    }
}
