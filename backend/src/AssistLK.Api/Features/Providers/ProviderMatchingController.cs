using AssistLK.Application.Interfaces;
using AssistLK.Application.Services.Providers;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace AssistLK.Api.Features.Providers;

public record ResumeMatchRequest(string Action);

[ApiController]
[Route("api/providers/match")]
[Authorize(Roles = "Admin")]
public class ProviderMatchingController : ControllerBase
{
    private readonly IProviderMatchingService _matchingService;
    private readonly IProviderMatchingCoordinator _matchingCoordinator;
    private readonly IServiceRequestService _serviceRequestService;
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IAgentWorkflowDbContext _dbContext;
    private readonly ILogger<ProviderMatchingController> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public ProviderMatchingController(
        IProviderMatchingService matchingService,
        IProviderMatchingCoordinator matchingCoordinator,
        IServiceRequestService serviceRequestService,
        IServiceRequestRepository serviceRequestRepository,
        IAgentWorkflowDbContext dbContext,
        ILogger<ProviderMatchingController> logger,
        IServiceScopeFactory scopeFactory)
    {
        _matchingService = matchingService;
        _matchingCoordinator = matchingCoordinator;
        _serviceRequestService = serviceRequestService;
        _serviceRequestRepository = serviceRequestRepository;
        _dbContext = dbContext;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    [NonAction]
    public async Task<MatchingExecutionResult> ExecuteMatchForRequestAsync(Guid serviceRequestId, CancellationToken cancellationToken)
    {
        return await _matchingCoordinator.ExecuteMatchForRequestAsync(serviceRequestId, cancellationToken);
    }

    [HttpPost("{serviceRequestId:guid}/start")]
    public async Task<IActionResult> StartMatching(Guid serviceRequestId, CancellationToken cancellationToken)
    {
        var result = await ExecuteMatchForRequestAsync(serviceRequestId, cancellationToken);
        if (result.Status == "NotReady")
        {
            return NotFound("Service Request not found or not ready for matching.");
        }

        if (result.Status == "Failed")
        {
            return StatusCode(503, result.Message ?? "AI Matching Engine is currently unavailable.");
        }

        return Ok(new
        {
            threadId = result.ThreadId,
            status = result.Status,
            message = result.Message,
            tokensConsumed = result.TokensConsumed
        });
    }

    [HttpPost("sync-ready")]
    public async Task<IActionResult> SyncReadyRequests(CancellationToken cancellationToken)
    {
        var readyRequests = await _serviceRequestRepository.GetByStatusAsync(
            ServiceRequestStatus.ReadyForMatching, 
            cancellationToken);

        if (readyRequests.Count == 0)
        {
            return Ok(new { message = "No requests in ReadyForMatching status.", totalDispatched = 0, results = Array.Empty<object>() });
        }

        // Filter out expired matching requests per Component 1 lifecycle contract
        var utcNow = DateTime.UtcNow;
        var eligibleReadyRequests = readyRequests
            .Where(r => r.MatchingExpiresAtUtc.HasValue && r.MatchingExpiresAtUtc.Value > utcNow)
            .ToList();

        if (eligibleReadyRequests.Count == 0)
        {
            return Ok(new { message = "No eligible requests in ReadyForMatching status (matching window expired).", totalDispatched = 0, results = Array.Empty<object>() });
        }

        var readyIds = eligibleReadyRequests.Select(r => r.Id).ToList();

        var acceptedSrIds = await _dbContext.MatchedCandidates
            .Where(m => readyIds.Contains(m.MatchingExecution.ServiceRequestId) &&
                        m.Status == MatchedCandidateStatus.Accepted)
            .Select(m => m.MatchingExecution.ServiceRequestId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var activeSrIds = await _dbContext.MatchingExecutions
            .Where(e => readyIds.Contains(e.ServiceRequestId) &&
                       (e.Status == MatchingExecutionStatus.Running 
                     || e.Status == MatchingExecutionStatus.PendingApproval 
                     || (e.Status == MatchingExecutionStatus.Completed && e.Candidates.Any(c => c.Status == MatchedCandidateStatus.Recommended))))
            .Select(e => e.ServiceRequestId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var excludedIds = acceptedSrIds.Concat(activeSrIds).ToHashSet();
        var toMatchIds = readyIds.Where(id => !excludedIds.Contains(id)).ToList();
        var results = new List<object>();

        foreach (var id in toMatchIds)
        {
            var matchResult = await ExecuteMatchForRequestAsync(id, cancellationToken);
            results.Add(new
            {
                serviceRequestId = id,
                status = matchResult.Status,
                threadId = matchResult.ThreadId,
                message = matchResult.Message
            });
        }

        return Ok(new
        {
            message = $"Processed {toMatchIds.Count} ready request(s).",
            totalDispatched = toMatchIds.Count,
            results = results
        });
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingApprovals(CancellationToken cancellationToken)
    {
        // 1. Purge/clean-up: update any old orphaned PendingApproval runs without valid candidates to Failed (bounded to 20 per call)
        var orphaned = await _dbContext.MatchingExecutions
            .Where(e => e.Status == MatchingExecutionStatus.PendingApproval 
                     && !e.Candidates.Any(c => c.ProviderId != Guid.Empty))
            .Take(20)
            .ToListAsync(cancellationToken);

        if (orphaned.Count > 0)
        {
            foreach (var o in orphaned)
            {
                o.Status = MatchingExecutionStatus.Failed;
                o.CompletedAt = DateTime.UtcNow;
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // 2. Fetch pending executions that strictly have at least one matched candidate (AsNoTracking for fast reads)
        var pendingExecutions = await _dbContext.MatchingExecutions
            .AsNoTracking()
            .Include(e => e.Candidates)
                .ThenInclude(c => c.Provider)
                    .ThenInclude(p => p.User)
            .Where(e => e.Status == MatchingExecutionStatus.PendingApproval 
                     && e.Candidates.Any(c => c.ProviderId != Guid.Empty))
            .OrderByDescending(e => e.StartedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (pendingExecutions.Count == 0)
        {
            return Ok(new List<object>());
        }

        // 3. Batch fetch related ServiceRequests
        var srIds = pendingExecutions.Select(e => e.ServiceRequestId).Distinct().ToList();
        var srMap = new Dictionary<Guid, AssistLK.Application.ServiceRequests.DTOs.ServiceRequestResponse>();
        foreach (var id in srIds)
        {
            try
            {
                var sr = await _serviceRequestService.GetByIdForAdminAsync(id, cancellationToken);
                if (sr != null)
                {
                    srMap[id] = sr;
                }
            }
            catch
            {
                // Ignore if not found, mapped gracefully
            }
        }

        // 4. Batch fetch provider occupancy status across pending and active jobs
        var topCandidateList = pendingExecutions
            .Select(e => e.Candidates?
                .Where(c => c.ProviderId != Guid.Empty)
                .OrderBy(c => c.Rank)
                .FirstOrDefault())
            .Where(c => c != null)
            .ToList();

        var providerIds = topCandidateList.Select(c => c!.ProviderId).Distinct().ToList();
        var busyProviderIds = new HashSet<Guid>();
        var reviewingProviderIds = new HashSet<Guid>();

        if (providerIds.Count > 0)
        {
            busyProviderIds = (await _dbContext.MatchedCandidates
                .AsNoTracking()
                .Where(m => providerIds.Contains(m.ProviderId) && m.Status == MatchedCandidateStatus.Accepted)
                .Select(m => m.ProviderId)
                .ToListAsync(cancellationToken))
                .ToHashSet();

            var reviewingCutoff = DateTime.UtcNow.AddSeconds(-65);
            reviewingProviderIds = (await _dbContext.MatchedCandidates
                .AsNoTracking()
                .Where(m => providerIds.Contains(m.ProviderId) &&
                            m.Status == MatchedCandidateStatus.Recommended &&
                            m.MatchingExecution != null &&
                            m.MatchingExecution.Status == MatchingExecutionStatus.Completed &&
                            m.CreatedAt >= reviewingCutoff)
                .Select(m => m.ProviderId)
                .ToListAsync(cancellationToken))
                .ToHashSet();
        }

        var results = new List<object>();

        foreach (var e in pendingExecutions)
        {
            var topCandidate = e.Candidates?
                .Where(c => c.ProviderId != Guid.Empty)
                .OrderBy(c => c.Rank)
                .FirstOrDefault();

            if (topCandidate == null)
            {
                continue;
            }

            srMap.TryGetValue(e.ServiceRequestId, out var sr);

            var techName = !string.IsNullOrWhiteSpace(topCandidate.Provider?.User?.FullName)
                ? topCandidate.Provider.User.FullName
                : (!string.IsNullOrWhiteSpace(topCandidate.Provider?.BusinessName)
                    ? topCandidate.Provider.BusinessName
                    : "Assigned Specialist");

            var hasOngoingJob = busyProviderIds.Contains(topCandidate.ProviderId);
            var hasReviewingDispatch = reviewingProviderIds.Contains(topCandidate.ProviderId);

            string occupancyStatus = "Available";
            string? occupancyReason = null;
            if (hasOngoingJob)
            {
                occupancyStatus = "BusyOnJob";
                occupancyReason = "Provider is currently attending to an active job in progress. Cannot dispatch another request until completed.";
            }
            else if (hasReviewingDispatch)
            {
                occupancyStatus = "ReviewingDispatch";
                occupancyReason = "Provider is currently reviewing a dispatched request countdown. Awaiting provider response (accept/decline).";
            }

            results.Add(new
            {
                threadId = e.ThreadId,
                serviceRequest = new 
                {
                    problemSummary = sr?.LatestAnalysis?.DetectedProblem ?? sr?.Description ?? "Service Request",
                    tradeCategory = sr?.Category ?? "General",
                    urgency = sr?.Urgency.ToString() ?? "Medium"
                },
                candidate = new 
                {
                    providerId = topCandidate.ProviderId,
                    technicianName = techName,
                    businessName = topCandidate.Provider?.BusinessName ?? techName,
                    rating = topCandidate.Provider?.Rating ?? 0.0m,
                    distanceKm = topCandidate.DistanceKm,
                    occupancyStatus = occupancyStatus,
                    occupancyReason = occupancyReason
                },
                aiRationale = !string.IsNullOrWhiteSpace(topCandidate.MatchRationale)
                    ? topCandidate.MatchRationale
                    : "Recommended candidate qualified based on skills and proximity.",
                tokenUsage = (object?)null
            });
        }

        return Ok(results);
    }

    [HttpPost("{threadId}/resume")]
    public async Task<IActionResult> ResumeMatching(string threadId, [FromBody] ResumeMatchRequest request, CancellationToken cancellationToken)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        var execution = await _dbContext.MatchingExecutions
            .Include(e => e.Candidates)
            .FirstOrDefaultAsync(e => e.ThreadId == threadId, cancellationToken);

        if (execution == null)
        {
            return NotFound("Matching execution thread not found.");
        }

        var isApproved = request.Action.Equals("Approve", StringComparison.OrdinalIgnoreCase);

        // Guard: Prevent approving if the candidate provider is occupied with an active job or reviewing another dispatch
        if (isApproved)
        {
            var topCandidate = execution.Candidates?
                .Where(c => c.ProviderId != Guid.Empty)
                .OrderBy(c => c.Rank)
                .FirstOrDefault();

            if (topCandidate != null)
            {
                var isBusy = await _dbContext.MatchedCandidates.AnyAsync(m =>
                    m.ProviderId == topCandidate.ProviderId &&
                    m.Id != topCandidate.Id &&
                    m.Status == MatchedCandidateStatus.Accepted,
                    cancellationToken);

                if (isBusy)
                {
                    return Conflict(new { message = "Provider is currently busy attending an active job. Cannot dispatch another request until the active job is completed." });
                }

                var isReviewing = await _dbContext.MatchedCandidates.AnyAsync(m =>
                    m.ProviderId == topCandidate.ProviderId &&
                    m.Id != topCandidate.Id &&
                    m.Status == MatchedCandidateStatus.Recommended &&
                    m.MatchingExecution != null &&
                    m.MatchingExecution.Status == MatchingExecutionStatus.Completed &&
                    m.CreatedAt >= DateTime.UtcNow.AddSeconds(-65),
                    cancellationToken);

                if (isReviewing)
                {
                    return Conflict(new { message = "Provider is currently reviewing another pending dispatch. Please wait until the provider accepts or declines." });
                }
            }
        }

        try
        {
            var response = await _matchingService.ResumeMatchingAsync(threadId, request.Action, adminId);

            execution.Status = isApproved
                ? MatchingExecutionStatus.Completed
                : MatchingExecutionStatus.Failed;

            execution.CompletedAt = DateTime.UtcNow;

            if (!isApproved && execution.Candidates != null)
            {
                foreach (var candidate in execution.Candidates)
                {
                    candidate.Status = MatchedCandidateStatus.Declined;
                    candidate.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            if (!isApproved)
            {
                var srId = execution.ServiceRequestId;
                _logger.LogInformation("Admin rejected match on thread {ThreadId}. Triggering cascade re-match for ServiceRequest {ServiceRequestId}.", threadId, srId);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var coordinator = scope.ServiceProvider.GetRequiredService<IProviderMatchingCoordinator>();
                        var result = await coordinator.ExecuteMatchForRequestAsync(srId);
                        _logger.LogInformation("Cascade re-matching after Admin rejection concluded for ServiceRequest {ServiceRequestId} with status: {Status} (Success={Success}).", srId, result.Status, result.Success);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Cascade re-matching failed after Admin rejection for ServiceRequest {ServiceRequestId}.", srId);
                    }
                });
            }

            return Ok(response);
        }
        catch (ApplicationException ex)
        {
            _logger.LogError(ex, "Matching engine unavailable on resume.");
            return StatusCode(503, "Could not process Admin match decision (service unavailable).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during match resume.");
            return StatusCode(500, "An internal error occurred.");
        }
    }
}