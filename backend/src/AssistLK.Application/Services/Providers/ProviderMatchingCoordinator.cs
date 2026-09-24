using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AssistLK.Application.Interfaces;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AssistLK.Application.Services.Providers;

public class ProviderMatchingCoordinator : IProviderMatchingCoordinator
{
    private readonly IProviderMatchingService _matchingService;
    private readonly IServiceRequestService _serviceRequestService;
    private readonly IAgentWorkflowDbContext _dbContext;
    private readonly ILogger<ProviderMatchingCoordinator> _logger;

    public ProviderMatchingCoordinator(
        IProviderMatchingService matchingService,
        IServiceRequestService serviceRequestService,
        IAgentWorkflowDbContext dbContext,
        ILogger<ProviderMatchingCoordinator> logger)
    {
        _matchingService = matchingService;
        _serviceRequestService = serviceRequestService;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<MatchingExecutionResult> ExecuteMatchForRequestAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default)
    {
        // 1. Guard against duplicate / concurrent runs for this request
        var existingActive = await _dbContext.MatchingExecutions
            .AnyAsync(e => e.ServiceRequestId == serviceRequestId 
                        && (e.Status == MatchingExecutionStatus.Running || e.Status == MatchingExecutionStatus.PendingApproval),
                      cancellationToken);

        if (existingActive)
        {
            _logger.LogInformation("Matching for ServiceRequest {ServiceRequestId} is already in progress or awaiting approval.", serviceRequestId);
            return new MatchingExecutionResult
            {
                Success = true,
                Status = "AlreadyActive",
                Message = "An active matching execution is already in progress or pending approval."
            };
        }

        // 2. Validate request is ready for matching
        var sr = await _serviceRequestService.GetReadyForMatchingAsync(serviceRequestId, cancellationToken);
        if (sr == null)
        {
            _logger.LogWarning("ServiceRequest {ServiceRequestId} not found or not in ReadyForMatching status.", serviceRequestId);
            return new MatchingExecutionResult
            {
                Success = false,
                Status = "NotReady",
                Message = "Service request not found or not ready for matching."
            };
        }

        int urgencyScore = sr.Urgency == ServiceRequestUrgency.Unknown ? 2 : (int)sr.Urgency;
        var objective = $"Category: {sr.Category}, Urgency: {urgencyScore}, Problem: {sr.ProblemSummary}, Location: {sr.LocationText}";

        // 3. Fetch live active, verified providers directly from database matching request category
        var targetCat = (sr.Category ?? string.Empty).Trim().ToLowerInvariant();
        bool isVehicle = targetCat.Contains("vehicle");
        bool isPlumbing = targetCat.Contains("plumb");
        bool isElectrical = targetCat.Contains("electr");
        bool isAppliance = targetCat.Contains("appliance");

        var eligibleQuery = _dbContext.ProviderProfiles
            .Include(p => p.Locations)
            .Include(p => p.Skills)
            .Where(p => p.IsOnline && p.VerificationStatus == ProviderVerificationStatus.Verified);

        if (isVehicle)
        {
            eligibleQuery = eligibleQuery.Where(p => p.Skills.Any(s => 
                s.Category.ToLower().Contains("vehicle") || s.SkillName.ToLower().Contains("vehicle")));
        }
        else if (isPlumbing)
        {
            eligibleQuery = eligibleQuery.Where(p => p.Skills.Any(s => 
                s.Category.ToLower().Contains("plumb") || s.SkillName.ToLower().Contains("plumb") || s.SkillName.ToLower().Contains("pipe") || s.SkillName.ToLower().Contains("leak")));
        }
        else if (isElectrical)
        {
            eligibleQuery = eligibleQuery.Where(p => p.Skills.Any(s => 
                s.Category.ToLower().Contains("electr") || s.SkillName.ToLower().Contains("electr") || s.SkillName.ToLower().Contains("wire") || s.SkillName.ToLower().Contains("circuit")));
        }
        else if (isAppliance)
        {
            eligibleQuery = eligibleQuery.Where(p => p.Skills.Any(s => 
                s.Category.ToLower().Contains("appliance") || s.SkillName.ToLower().Contains("appliance")));
        }
        else if (!string.IsNullOrWhiteSpace(sr.Category) && sr.Category != "Unclassified")
        {
            eligibleQuery = eligibleQuery.Where(p => p.Skills.Any(s => 
                s.Category.ToLower() == targetCat || s.SkillName.ToLower().Contains(targetCat)));
        }

        var eligibleProviders = await eligibleQuery
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

            if (matchResponse.Status == "No_Eligible_Providers" || matchResponse.RecommendedProvider == null)
            {
                execution.Status = MatchingExecutionStatus.Failed;
                execution.CompletedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);

                var emptyMsg = !string.IsNullOrWhiteSpace(matchResponse.Message)
                    ? matchResponse.Message
                    : "No eligible providers found within their operational radius.";

                return new MatchingExecutionResult
                {
                    Success = false,
                    Status = matchResponse.Status,
                    ThreadId = matchResponse.ThreadId,
                    Message = emptyMsg,
                    MatchingExecutionId = execution.Id,
                    TokensConsumed = matchResponse.TokensConsumed
                };
            }

            execution.Status = MatchingExecutionStatus.PendingApproval;

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

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("ServiceRequest {ServiceRequestId} successfully paused at LangGraph approval gate with thread {ThreadId}.", serviceRequestId, matchResponse.ThreadId);

            return new MatchingExecutionResult
            {
                Success = true,
                Status = matchResponse.Status,
                ThreadId = matchResponse.ThreadId,
                MatchingExecutionId = execution.Id,
                TokensConsumed = matchResponse.TokensConsumed
            };
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "HTTP error communicating with Python matching service for ServiceRequest {ServiceRequestId}", serviceRequestId);
            execution.Status = MatchingExecutionStatus.Failed;
            execution.CompletedAt = DateTime.UtcNow;
            try { await _dbContext.SaveChangesAsync(cancellationToken); } catch { }

            return new MatchingExecutionResult
            {
                Success = false,
                Status = "Failed",
                Message = "AI Matching Engine HTTP communication error.",
                MatchingExecutionId = execution.Id
            };
        }
        catch (TaskCanceledException timeoutEx)
        {
            _logger.LogError(timeoutEx, "Timeout communicating with Python matching service for ServiceRequest {ServiceRequestId}", serviceRequestId);
            execution.Status = MatchingExecutionStatus.Failed;
            execution.CompletedAt = DateTime.UtcNow;
            try { await _dbContext.SaveChangesAsync(cancellationToken); } catch { }

            return new MatchingExecutionResult
            {
                Success = false,
                Status = "Failed",
                Message = "AI Matching Engine timed out.",
                MatchingExecutionId = execution.Id
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while executing matching for ServiceRequest {ServiceRequestId}", serviceRequestId);

            execution.Status = MatchingExecutionStatus.Failed;
            execution.CompletedAt = DateTime.UtcNow;
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx, "Failed to update execution status to Failed for {ExecutionId}", execution.Id);
            }

            return new MatchingExecutionResult
            {
                Success = false,
                Status = "Failed",
                Message = ex.Message,
                MatchingExecutionId = execution.Id
            };
        }
    }
}
