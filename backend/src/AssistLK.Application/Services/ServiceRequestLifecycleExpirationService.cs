using AssistLK.Application.Interfaces;
using AssistLK.Application.ServiceRequests;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AssistLK.Application.Services;

/// <summary>
/// Service responsible for identifying and safely cancelling stale AwaitingInformation ServiceRequests
/// whose actionable clarification questions have remained unanswered beyond the configured timeout.
/// ReadyForMatching requests are NEVER modified by this service.
/// </summary>
public class ServiceRequestLifecycleExpirationService : IServiceRequestLifecycleExpirationService
{
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IAgentWorkflowDbContext? _workflowDbContext;
    private readonly ServiceRequestLifecycleOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ServiceRequestLifecycleExpirationService> _logger;

    public ServiceRequestLifecycleExpirationService(
        IServiceRequestRepository serviceRequestRepository,
        IAgentWorkflowDbContext? workflowDbContext = null,
        ServiceRequestLifecycleOptions? directOptions = null,
        IOptions<ServiceRequestLifecycleOptions>? optionsWrapper = null,
        TimeProvider? timeProvider = null,
        ILogger<ServiceRequestLifecycleExpirationService>? logger = null)
    {
        _serviceRequestRepository = serviceRequestRepository;
        _workflowDbContext = workflowDbContext;
        _options = directOptions ?? optionsWrapper?.Value ?? new ServiceRequestLifecycleOptions();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<ServiceRequestLifecycleExpirationService>.Instance;
    }

    /// <summary>
    /// Scans for stale AwaitingInformation requests and cancels eligible requests.
    /// Returns the number of cancelled requests.
    /// </summary>
    public async Task<int> ExpireStaleAwaitingInformationRequestsAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("Service request lifecycle expiration is disabled by configuration.");
            return 0;
        }

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var cutoffTimeUtc = nowUtc.Subtract(TimeSpan.FromHours(_options.AwaitingInformationTimeoutHours));

        var candidates = await _serviceRequestRepository.GetStaleAwaitingInformationRequestsAsync(
            cutoffTimeUtc,
            cancellationToken);

        if (candidates.Count == 0)
        {
            return 0;
        }

        int expiredCount = 0;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var expired = await ExpireStaleAwaitingInformationRequestCoreAsync(
                    candidate.Id,
                    cutoffTimeUtc,
                    cancellationToken);

                if (expired)
                {
                    expiredCount++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error processing lifecycle expiration for service request {ServiceRequestId}.",
                    candidate.Id);
            }
        }

        return expiredCount;
    }

    /// <summary>
    /// Evaluates and cancels a specific service request if it is eligible for AwaitingInformation timeout.
    /// Returns true if cancelled, or false if not eligible or concurrently advanced.
    /// </summary>
    public async Task<bool> ExpireStaleAwaitingInformationRequestAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return false;
        }

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var cutoffTimeUtc = nowUtc.Subtract(TimeSpan.FromHours(_options.AwaitingInformationTimeoutHours));

        return await ExpireStaleAwaitingInformationRequestCoreAsync(
            serviceRequestId,
            cutoffTimeUtc,
            cancellationToken);
    }

    private async Task<bool> ExpireStaleAwaitingInformationRequestCoreAsync(
        Guid serviceRequestId,
        DateTime cutoffTimeUtc,
        CancellationToken cancellationToken)
    {
        // 1. Reload the latest persisted state directly from storage to prevent operating on stale cache
        var request = await _serviceRequestRepository.ReloadForRecoveryAsync(serviceRequestId, cancellationToken);
        if (request is null)
        {
            _logger.LogDebug(
                "ServiceRequest {ServiceRequestId} was not found during expiration check.",
                serviceRequestId);
            return false;
        }

        // 2. Strict eligibility validation: MUST be AwaitingInformation
        // Explicit safeguard: ReadyForMatching requests are NEVER modified
        if (request.Status != ServiceRequestStatus.AwaitingInformation)
        {
            _logger.LogDebug(
                "ServiceRequest {ServiceRequestId} is not in AwaitingInformation status (current: {Status}). Skipping expiration.",
                serviceRequestId,
                request.Status);
            return false;
        }

        // Ensure navigation collections are populated
        if (request.Clarifications == null)
        {
            request = await _serviceRequestRepository.GetByIdAsync(
                serviceRequestId,
                includeProblemAnalyses: false,
                includeClarifications: true,
                cancellationToken: cancellationToken);

            if (request is null || request.Status != ServiceRequestStatus.AwaitingInformation)
            {
                return false;
            }
        }

        // 3. Conservative guard: verify timeout is still exceeded based on UpdatedAt
        if (request.UpdatedAt > cutoffTimeUtc)
        {
            _logger.LogDebug(
                "ServiceRequest {ServiceRequestId} has been updated recently ({UpdatedAtUtc} > {CutoffUtc}). Skipping expiration.",
                serviceRequestId,
                request.UpdatedAt,
                cutoffTimeUtc);
            return false;
        }

        // 4. Actionable unanswered clarification check:
        // Must have an unanswered, non-superseded clarification question
        var hasActionableUnansweredClarification = request.Clarifications != null && request.Clarifications.Any(c =>
            string.IsNullOrWhiteSpace(c.Answer) && c.SupersededAt == null);

        if (!hasActionableUnansweredClarification)
        {
            _logger.LogDebug(
                "ServiceRequest {ServiceRequestId} has no actionable unanswered clarifications. Skipping expiration.",
                serviceRequestId);
            return false;
        }

        // 5. Apply state transition to Cancelled
        request.Status = ServiceRequestStatus.Cancelled;
        _serviceRequestRepository.Update(request);

        // 6. Record audit log if workflow DB context is available
        if (_workflowDbContext != null)
        {
            try
            {
                var requestIdStr = serviceRequestId.ToString();
                var activeWorkflow = await _workflowDbContext.AgentWorkflows
                    .Where(w => w.WorkflowType == "ProblemUnderstanding" && w.Input == requestIdStr)
                    .OrderByDescending(w => w.UpdatedAtUtc)
                    .FirstOrDefaultAsync(cancellationToken);

                if (activeWorkflow != null)
                {
                    _workflowDbContext.AgentAuditLogs.Add(new AgentAuditLog
                    {
                        Id = Guid.NewGuid(),
                        WorkflowId = activeWorkflow.Id,
                        UserId = request.CustomerId,
                        EventType = "ClarificationTimeout",
                        Description = $"Service request automatically cancelled due to clarification timeout exceeding {_options.AwaitingInformationTimeoutHours} hours.",
                        CreatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime
                    });
                    await _workflowDbContext.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to record AgentAuditLog for service request {ServiceRequestId} clarification timeout.",
                    serviceRequestId);
            }
        }

        // 7. Save changes with optimistic concurrency safeguard
        try
        {
            await _serviceRequestRepository.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "ServiceRequest {ServiceRequestId} transitioned to Cancelled due to clarification timeout ({TimeoutHours}h exceeded). Reason: {Reason}.",
                serviceRequestId,
                _options.AwaitingInformationTimeoutHours,
                "ClarificationTimeout");

            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogInformation(
                ex,
                "ServiceRequest {ServiceRequestId} was concurrently updated while attempting timeout cancellation. Cancellation safely aborted.",
                serviceRequestId);
            return false;
        }
    }
}
