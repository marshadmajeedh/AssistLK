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
/// Service responsible for identifying and safely recovering genuinely stale or crashed
/// ServiceRequests in Analyzing status back to a safe pre-analysis status (Created or AwaitingInformation).
/// </summary>
public class StaleAnalysisRecoveryService : IStaleAnalysisRecoveryService
{
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IAgentWorkflowDbContext? _workflowDbContext;
    private readonly C1RecoveryOptions _options;
    private readonly ILogger<StaleAnalysisRecoveryService> _logger;

    public StaleAnalysisRecoveryService(
        IServiceRequestRepository serviceRequestRepository,
        IAgentWorkflowDbContext? workflowDbContext = null,
        C1RecoveryOptions? directOptions = null,
        IOptions<C1RecoveryOptions>? optionsWrapper = null,
        ILogger<StaleAnalysisRecoveryService>? logger = null)
    {
        _serviceRequestRepository = serviceRequestRepository;
        _workflowDbContext = workflowDbContext;
        _options = directOptions ?? optionsWrapper?.Value ?? new C1RecoveryOptions();
        _logger = logger ?? NullLogger<StaleAnalysisRecoveryService>.Instance;
    }

    /// <summary>
    /// Scans for stale Analyzing requests and recovers eligible requests to their safe pre-analysis status.
    /// Returns the number of recovered requests.
    /// </summary>
    public async Task<int> RecoverStaleAnalysesAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("Stale analysis recovery is disabled by configuration.");
            return 0;
        }

        var cutoffTimeUtc = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(_options.StaleAnalysisMinutes));
        var candidates = await _serviceRequestRepository.GetStaleAnalyzingRequestsAsync(cutoffTimeUtc, cancellationToken);

        if (candidates.Count == 0)
        {
            return 0;
        }

        int recoveredCount = 0;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var recovered = await RecoverStaleRequestCoreAsync(candidate.Id, cutoffTimeUtc, cancellationToken);
            if (recovered)
            {
                recoveredCount++;
            }
        }

        return recoveredCount;
    }

    /// <summary>
    /// Recovers a specific service request if it is eligible for stale recovery.
    /// Returns true if recovered, or false if not eligible or concurrently advanced.
    /// </summary>
    public async Task<bool> RecoverStaleRequestAsync(Guid serviceRequestId, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return false;
        }

        var cutoffTimeUtc = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(_options.StaleAnalysisMinutes));
        return await RecoverStaleRequestCoreAsync(serviceRequestId, cutoffTimeUtc, cancellationToken);
    }

    private async Task<bool> RecoverStaleRequestCoreAsync(
        Guid serviceRequestId,
        DateTime cutoffTimeUtc,
        CancellationToken cancellationToken)
    {
        // 1. Reload the latest persisted state directly from storage to prevent operating on stale cache
        var request = await _serviceRequestRepository.ReloadForRecoveryAsync(serviceRequestId, cancellationToken);
        if (request is null)
        {
            _logger.LogDebug("Service request {ServiceRequestId} was not found during recovery check.", serviceRequestId);
            return false;
        }

        // 2. Strict eligibility validation
        if (request.Status != ServiceRequestStatus.Analyzing)
        {
            _logger.LogDebug(
                "ServiceRequest {ServiceRequestId} is no longer in Analyzing status (current: {Status}). Skipping recovery.",
                serviceRequestId,
                request.Status);
            return false;
        }

        // Conservative guard: Ensure request has genuinely exceeded the stale threshold
        if (request.UpdatedAt > cutoffTimeUtc)
        {
            _logger.LogDebug(
                "ServiceRequest {ServiceRequestId} is not stale (UpdatedAt: {UpdatedAtUtc} > Cutoff: {CutoffUtc}). Skipping recovery.",
                serviceRequestId,
                request.UpdatedAt,
                cutoffTimeUtc);
            return false;
        }

        // Double check it's not Cancelled or ReadyForMatching
        if (request.Status is ServiceRequestStatus.Cancelled or ServiceRequestStatus.ReadyForMatching)
        {
            return false;
        }

        // Ensure navigation collections are populated
        if (request.ProblemAnalyses == null || request.Clarifications == null)
        {
            request = await _serviceRequestRepository.GetByIdAsync(
                serviceRequestId,
                includeProblemAnalyses: true,
                includeClarifications: true,
                cancellationToken: cancellationToken);

            if (request is null || request.Status != ServiceRequestStatus.Analyzing)
            {
                return false;
            }
        }

        // 3. Guard against downgrading a request that has a valid completed ProblemAnalysis for the current revision
        var hasValidCurrentAnalysis = request.ProblemAnalyses != null && request.ProblemAnalyses.Any(a =>
            a.EvidenceRevision == request.EvidenceRevision &&
            a.Confidence > 0m &&
            !string.IsNullOrWhiteSpace(a.DetectedProblem));

        if (hasValidCurrentAnalysis)
        {
            _logger.LogWarning(
                "ServiceRequest {ServiceRequestId} has a completed ProblemAnalysis for EvidenceRevision {EvidenceRevision} despite Analyzing status. Skipping recovery to preserve analysis.",
                serviceRequestId,
                request.EvidenceRevision);
            return false;
        }

        // 4. Check if an AgentWorkflow for this request is still actively running and recent
        if (_workflowDbContext != null)
        {
            var requestIdStr = serviceRequestId.ToString();
            var activeWorkflow = await _workflowDbContext.AgentWorkflows
                .Where(w => w.WorkflowType == "ProblemUnderstanding" && w.Input == requestIdStr)
                .OrderByDescending(w => w.UpdatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (activeWorkflow != null && activeWorkflow.Status == "Running" && activeWorkflow.UpdatedAtUtc > cutoffTimeUtc)
            {
                _logger.LogInformation(
                    "ServiceRequest {ServiceRequestId} has active AgentWorkflow {WorkflowId} updated recently at {WorkflowUpdatedAtUtc}. Skipping recovery.",
                    serviceRequestId,
                    activeWorkflow.Id,
                    activeWorkflow.UpdatedAtUtc);
                return false;
            }
        }

        // 5. Determine safe pre-analysis status
        // Recover to AwaitingInformation ONLY when at least one current actionable clarification exists
        // where Answer == null/whitespace and SupersededAt == null. Otherwise recover to Created.
        var hasActionableUnresolvedClarification = request.Clarifications != null && request.Clarifications.Any(c =>
            string.IsNullOrWhiteSpace(c.Answer) && c.SupersededAt == null);

        var targetStatus = hasActionableUnresolvedClarification
            ? ServiceRequestStatus.AwaitingInformation
            : ServiceRequestStatus.Created;

        var previousStatus = request.Status;
        var staleDuration = DateTime.UtcNow - request.UpdatedAt;

        // 6. Apply state transition with concurrency token protection
        request.Status = targetStatus;
        _serviceRequestRepository.Update(request);

        try
        {
            await _serviceRequestRepository.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Recovered stale service request {ServiceRequestId} from {PreviousStatus} to {TargetStatus}. Reason: {RecoveryReason}. StaleDuration: {StaleDurationMinutes:F1} minutes.",
                serviceRequestId,
                previousStatus,
                targetStatus,
                "Stale analysis timeout exceeded; no active workflow detected",
                staleDuration.TotalMinutes);

            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogInformation(
                ex,
                "ServiceRequest {ServiceRequestId} was concurrently updated while attempting recovery. Downgrade safely prevented.",
                serviceRequestId);
            return false;
        }
    }
}
