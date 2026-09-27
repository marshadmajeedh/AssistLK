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

    public ProviderMatchingController(
        IProviderMatchingService matchingService,
        IProviderMatchingCoordinator matchingCoordinator,
        IServiceRequestService serviceRequestService,
        IServiceRequestRepository serviceRequestRepository,
        IAgentWorkflowDbContext dbContext,
        ILogger<ProviderMatchingController> logger)
    {
        _matchingService = matchingService;
        _matchingCoordinator = matchingCoordinator;
        _serviceRequestService = serviceRequestService;
        _serviceRequestRepository = serviceRequestRepository;
        _dbContext = dbContext;
        _logger = logger;
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

        var readyIds = readyRequests.Select(r => r.Id).ToList();

        var activeOrCompletedSrIds = await _dbContext.MatchingExecutions
            .Where(e => readyIds.Contains(e.ServiceRequestId) &&
                       (e.Status == MatchingExecutionStatus.Running 
                     || e.Status == MatchingExecutionStatus.PendingApproval 
                     || e.Status == MatchingExecutionStatus.Completed))
            .Select(e => e.ServiceRequestId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var toMatchIds = readyIds.Except(activeOrCompletedSrIds).ToList();
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
        // 1. Purge/clean-up: update any old orphaned PendingApproval runs without valid candidates to Failed
        var orphaned = await _dbContext.MatchingExecutions
            .Include(e => e.Candidates)
            .Where(e => e.Status == MatchingExecutionStatus.PendingApproval 
                     && !e.Candidates.Any(c => c.ProviderId != Guid.Empty))
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

        // 2. Fetch pending executions that strictly have at least one matched candidate
        var pendingExecutions = await _dbContext.MatchingExecutions
            .Include(e => e.Candidates)
                .ThenInclude(c => c.Provider)
                    .ThenInclude(p => p.User)
            .Where(e => e.Status == MatchingExecutionStatus.PendingApproval 
                     && e.Candidates.Any(c => c.ProviderId != Guid.Empty))
            .OrderByDescending(e => e.StartedAt)
            .ToListAsync(cancellationToken);

        if (pendingExecutions.Count == 0)
        {
            return Ok(new List<object>());
        }

        var results = new List<object>();

        foreach (var e in pendingExecutions)
        {
            var topCandidate = e.Candidates
                .Where(c => c.ProviderId != Guid.Empty)
                .OrderBy(c => c.Rank)
                .FirstOrDefault();

            if (topCandidate == null)
            {
                continue;
            }
            
            // Try to fetch ServiceRequest safely
            AssistLK.Application.ServiceRequests.DTOs.ServiceRequestResponse? sr = null;
            try
            {
                sr = await _serviceRequestService.GetByIdForAdminAsync(e.ServiceRequestId, cancellationToken);
            }
            catch
            {
                // Ignore if not found, we will map it gracefully
            }
            
            var techName = !string.IsNullOrWhiteSpace(topCandidate.Provider?.User?.FullName)
                ? topCandidate.Provider.User.FullName
                : (!string.IsNullOrWhiteSpace(topCandidate.Provider?.BusinessName)
                    ? topCandidate.Provider.BusinessName
                    : "Assigned Specialist");

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
                    technicianName = techName,
                    businessName = topCandidate.Provider?.BusinessName ?? techName,
                    rating = topCandidate.Provider?.Rating ?? 5.0m,
                    distanceKm = topCandidate.DistanceKm
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

        try
        {
            var response = await _matchingService.ResumeMatchingAsync(threadId, request.Action, adminId);

            var isApproved = request.Action.Equals("Approve", StringComparison.OrdinalIgnoreCase);

            execution.Status = isApproved
                ? MatchingExecutionStatus.Completed
                : MatchingExecutionStatus.Failed;

            execution.CompletedAt = DateTime.UtcNow;

            if (!isApproved && execution.Candidates != null)
            {
                foreach (var candidate in execution.Candidates)
                {
                    candidate.Status = MatchedCandidateStatus.Declined;
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

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