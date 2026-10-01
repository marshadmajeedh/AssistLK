using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Net.Sockets;
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
    private const int MaxTransientRetries = 3;

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
                await ProcessTimedOutDispatchesAsync(stoppingToken);
                await ProcessReadyRequestsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in ProviderMatchingBackgroundWorker loop.");
            }
        }
    }

    private async Task ProcessTimedOutDispatchesAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IAgentWorkflowDbContext>();

        // 60-second dispatch acceptance timeout threshold
        var timeoutLimit = DateTime.UtcNow.AddSeconds(-60);

        var timedOutCandidates = await ExecuteWithTransientRetryAsync(
            () => dbContext.MatchedCandidates
                .Include(m => m.MatchingExecution)
                .Where(m => m.Status == MatchedCandidateStatus.Recommended
                         && m.MatchingExecution != null
                         && m.MatchingExecution.Status == MatchingExecutionStatus.Completed
                         && ((m.MatchingExecution.CompletedAt ?? m.CreatedAt) < timeoutLimit))
                .ToListAsync(stoppingToken),
            stoppingToken,
            "loading timed-out provider matches");

        if (timedOutCandidates.Count == 0) return;

        _logger.LogInformation("ProviderMatchingBackgroundWorker detected {Count} timed-out dispatch match(es).", timedOutCandidates.Count);

        foreach (var candidate in timedOutCandidates)
        {
            if (stoppingToken.IsCancellationRequested) break;

            var serviceRequestId = candidate.MatchingExecution.ServiceRequestId;
            _logger.LogInformation("Match candidate {CandidateId} for provider {ProviderId} timed out after 60s. Auto-redispatching ServiceRequest {ServiceRequestId}.",
                candidate.Id, candidate.ProviderId, serviceRequestId);

            candidate.Status = MatchedCandidateStatus.Declined;
            candidate.UpdatedAt = DateTime.UtcNow;
            candidate.MatchRationale = (candidate.MatchRationale ?? string.Empty) + " [Timed out after 60s without acceptance]";

            candidate.MatchingExecution.Status = MatchingExecutionStatus.Failed;
            candidate.MatchingExecution.CompletedAt = DateTime.UtcNow;

            await ExecuteWithTransientRetryAsync(
                () => dbContext.SaveChangesAsync(stoppingToken),
                stoppingToken,
                $"saving timeout for provider match {candidate.Id}");

            try
            {
                using var innerScope = _scopeFactory.CreateScope();
                var coordinator = innerScope.ServiceProvider.GetRequiredService<IProviderMatchingCoordinator>();
                var result = await coordinator.ExecuteMatchForRequestAsync(
                    serviceRequestId,
                    stoppingToken,
                    autoApprove: true);
                _logger.LogInformation("Cascade re-matching following timeout for ServiceRequest {ServiceRequestId} concluded with status: {Status} (Success={Success}).",
                    serviceRequestId, result.Status, result.Success);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cascade re-match following timeout for ServiceRequest {ServiceRequestId}.", serviceRequestId);
            }
        }
    }

    private async Task<T> ExecuteWithTransientRetryAsync<T>(
        Func<Task<T>> operation,
        CancellationToken stoppingToken,
        string operationDescription)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (IsTransientDatabaseException(ex) && attempt < MaxTransientRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                _logger.LogWarning(ex,
                    "Transient failure while {OperationDescription}. Retrying in {DelaySeconds} seconds (attempt {Attempt}/{MaxAttempts}).",
                    operationDescription, delay.TotalSeconds, attempt, MaxTransientRetries);
                await Task.Delay(delay, stoppingToken);
            }
        }
    }

    private static bool IsTransientDatabaseException(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current is SocketException || current is TimeoutException || current is IOException || current is DbException)
            {
                return true;
            }
        }

        return false;
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

        // 1. Exclude requests that already have an accepted candidate
        var acceptedSrIds = await dbContext.MatchedCandidates
            .Where(m => readyIds.Contains(m.MatchingExecution.ServiceRequestId) &&
                        m.Status == MatchedCandidateStatus.Accepted)
            .Select(m => m.MatchingExecution.ServiceRequestId)
            .Distinct()
            .ToListAsync(stoppingToken);

        // 2. Exclude requests that already have an active/pending/dispatched execution
        var activeSrIds = await dbContext.MatchingExecutions
            .Where(e => readyIds.Contains(e.ServiceRequestId) &&
                       (e.Status == MatchingExecutionStatus.Running
                     || e.Status == MatchingExecutionStatus.PendingApproval
                     || (e.Status == MatchingExecutionStatus.Completed && e.Candidates.Any(c => c.Status == MatchedCandidateStatus.Recommended))))
            .Select(e => e.ServiceRequestId)
            .Distinct()
            .ToListAsync(stoppingToken);

        // 3. Exclude requests that recently failed (within the last 2 minutes) to avoid spamming the AI engine
        var recentlyFailedSrIds = await dbContext.MatchingExecutions
            .Where(e => readyIds.Contains(e.ServiceRequestId) &&
                       e.Status == MatchingExecutionStatus.Failed &&
                       e.CompletedAt != null &&
                       e.CompletedAt > DateTime.UtcNow.AddMinutes(-2))
            .Select(e => e.ServiceRequestId)
            .Distinct()
            .ToListAsync(stoppingToken);

        var excludedIds = acceptedSrIds.Concat(activeSrIds).Concat(recentlyFailedSrIds).ToHashSet();
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
