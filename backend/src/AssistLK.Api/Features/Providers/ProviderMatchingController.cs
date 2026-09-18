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

        int urgencyScore = sr.Urgency == ServiceRequestUrgency.Unknown ? 2 : (int)sr.Urgency;
        var objective = $"Category: {sr.Category}, Urgency: {urgencyScore}, Problem: {sr.ProblemSummary}, Location: {sr.LocationText}";

        // 1. Fetch live active, verified providers directly from PostgreSQL
        var eligibleProviders = await _dbContext.ProviderProfiles
            .Include(p => p.Locations)
            .Include(p => p.Skills)
            .Where(p => p.IsOnline && p.VerificationStatus == ProviderVerificationStatus.Verified)
            .Select(p => new ProviderCandidateDto
            {
                ProviderId = p.Id.ToString(),
                Name = p.BusinessName,
                Rating = (double)p.Rating,
                Latitude = (double)(p.Locations.Select(l => l.Latitude).FirstOrDefault()),
                Longitude = (double)(p.Locations.Select(l => l.Longitude).FirstOrDefault()),
                OperatingRadiusKm = (double)(p.Locations.Select(l => l.OperatingRadiusKm).FirstOrDefault()),
                Verified = true,
                Skills = p.Skills.Select(s => s.SkillName.ToLower()).ToList()
            })
            .ToListAsync(cancellationToken);

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
            // 2. Dispatch real DB records and customer coordinates to Python
            var matchRequest = new MatchStartRequest
               {
                   Objective = objective,
                   Urgency = urgencyScore,
                   CustomerLatitude = (double)(sr.Latitude ?? 6.9270m),
                   CustomerLongitude = (double)(sr.Longitude ?? 79.8610m),
                   EligibleProviders = eligibleProviders
                };

            var matchResponse = await _matchingService.StartMatchingAsync(matchRequest);

            execution.ThreadId = matchResponse.ThreadId;
            execution.Status = MatchingExecutionStatus.PendingApproval;

            if (matchResponse.RecommendedProvider != null)
            {
                var rp = matchResponse.RecommendedProvider;

                if (!Guid.TryParse(rp.Id, out var providerGuid))
                {
                    providerGuid = eligibleProviders.Count > 0 
                        ? Guid.Parse(eligibleProviders[0].ProviderId) 
                        : Guid.NewGuid();
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

            return Ok(new 
{ 
    threadId = matchResponse.ThreadId, 
    status = matchResponse.Status,
    tokensConsumed = matchResponse.TokensConsumed
});
        }
        catch (ApplicationException ex)
        {
            _logger.LogError(ex, "Matching engine unavailable.");
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