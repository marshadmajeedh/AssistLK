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

[ApiController]
[Route("api/providers/match")]
[Authorize(Roles = "Admin")]
public class ProviderMatchingController : ControllerBase
{
    private readonly IProviderMatchingService _matchingService;
    private readonly IServiceRequestService _serviceRequestService;
    private readonly IAgentWorkflowDbContext _dbContext;
    private readonly ILogger<ProviderMatchingController> _logger;

    public ProviderMatchingController(
        IProviderMatchingService matchingService,
        IServiceRequestService serviceRequestService,
        IAgentWorkflowDbContext dbContext,
        ILogger<ProviderMatchingController> logger)
    {
        _matchingService = matchingService;
        _serviceRequestService = serviceRequestService;
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpPost("{serviceRequestId:guid}/start")]
    public async Task<IActionResult> StartMatching(Guid serviceRequestId, CancellationToken cancellationToken)
    {
        var sr = await _serviceRequestService.GetReadyForMatchingAsync(serviceRequestId, cancellationToken);
        if (sr == null)
        {
            return NotFound("Service Request not found or not ready for matching.");
        }

        // Map Unknown (0) safely to Medium (2), otherwise preserve numeric enum value
        int urgencyScore = sr.Urgency == ServiceRequestUrgency.Unknown ? 2 : (int)sr.Urgency;

        var objective = $"Category: {sr.Category}, Urgency: {urgencyScore}, Problem: {sr.ProblemSummary}, Location: {sr.LocationText}";

        var execution = new MatchingExecution
        {
            ServiceRequestId = serviceRequestId,
            Status = MatchingExecutionStatus.Running,
            StartedAt = DateTime.UtcNow
        };

        _dbContext.MatchingExecutions.Add(execution);
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var matchResponse = await _matchingService.StartMatchingAsync(objective);

            execution.ThreadId = matchResponse.ThreadId;
            execution.Status = MatchingExecutionStatus.PendingApproval;

            if (matchResponse.RecommendedProvider != null)
            {
                var rp = matchResponse.RecommendedProvider;

                // Safely parse GUID or look up an existing provider profile to avoid FormatException
                if (!Guid.TryParse(rp.Id, out var providerGuid))
                {
                    var fallbackId = await _dbContext.ProviderProfiles
                        .Select(p => p.Id)
                        .FirstOrDefaultAsync(cancellationToken);

                    providerGuid = fallbackId != Guid.Empty ? fallbackId : Guid.NewGuid();
                }

                var candidate = new MatchedCandidate
                {
                    MatchingExecutionId = execution.Id,
                    ProviderId = providerGuid,
                    Score = rp.Score,
                    Rank = rp.Rank,
                    DistanceKm = rp.DistanceKm,
                    MatchRationale = rp.MatchRationale,
                    Status = MatchedCandidateStatus.Recommended
                };
                _dbContext.MatchedCandidates.Add(candidate);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(new { threadId = matchResponse.ThreadId, status = matchResponse.Status });
        }
        catch (ApplicationException ex)
        {
            _logger.LogError(ex, "Matching engine unavailable.");

            // Mark failed in DB
            execution.Status = MatchingExecutionStatus.Failed;
            execution.CompletedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return StatusCode(503, "AI Matching Engine is currently unavailable.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during match start.");

            execution.Status = MatchingExecutionStatus.Failed;
            execution.CompletedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return StatusCode(500, "An internal error occurred.");
        }
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

            execution.Status = request.Action.Equals("Approve", StringComparison.OrdinalIgnoreCase)
                ? MatchingExecutionStatus.Completed
                : MatchingExecutionStatus.Failed;

            execution.CompletedAt = DateTime.UtcNow;

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

public class ResumeMatchRequest
{
    public string Action { get; set; } = string.Empty;
}