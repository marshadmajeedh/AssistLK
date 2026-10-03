using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AssistLK.Application.Interfaces;
using AssistLK.Application.Services;
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
    private readonly AgentWorkflowService _workflowService;
    private readonly AgentMonitoringService _monitoringService;

    public ProviderMatchingCoordinator(
        IProviderMatchingService matchingService,
        IServiceRequestService serviceRequestService,
        IAgentWorkflowDbContext dbContext,
        ILogger<ProviderMatchingCoordinator> logger,
        AgentWorkflowService workflowService,
        AgentMonitoringService monitoringService)
    {
        _matchingService = matchingService;
        _serviceRequestService = serviceRequestService;
        _dbContext = dbContext;
        _logger = logger;
        _workflowService = workflowService;
        _monitoringService = monitoringService;
    }

    public async Task<MatchingExecutionResult> ExecuteMatchForRequestAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default,
        bool autoApprove = false)
    {
        // 1. Guard against duplicate / concurrent runs or requests already accepted
        var alreadyAccepted = await _dbContext.MatchedCandidates
            .AnyAsync(m => m.MatchingExecution.ServiceRequestId == serviceRequestId 
                        && m.Status == MatchedCandidateStatus.Accepted,
                      cancellationToken);

        if (alreadyAccepted)
        {
            _logger.LogInformation("ServiceRequest {ServiceRequestId} already has an accepted provider match.", serviceRequestId);
            return new MatchingExecutionResult
            {
                Success = true,
                Status = "AlreadyAccepted",
                Message = "This service request has already been accepted by a provider."
            };
        }

        var existingActive = await _dbContext.MatchingExecutions
            .AnyAsync(e => e.ServiceRequestId == serviceRequestId 
                        && (e.Status == MatchingExecutionStatus.Running 
                            || e.Status == MatchingExecutionStatus.PendingApproval
                            || (e.Status == MatchingExecutionStatus.Completed && e.Candidates.Any(c => c.Status == MatchedCandidateStatus.Recommended))),
                      cancellationToken);

        if (existingActive)
        {
            _logger.LogInformation("Matching for ServiceRequest {ServiceRequestId} is already in progress, awaiting approval, or dispatched to a provider.", serviceRequestId);
            return new MatchingExecutionResult
            {
                Success = true,
                Status = "AlreadyActive",
                Message = "An active matching execution is already in progress or dispatched to a provider."
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

        // Validate matching window eligibility per Component 1 contract (24-hour window)
        var nowUtc = DateTime.UtcNow;
        bool is24hExpired = sr.CreatedAt < nowUtc.AddHours(-24) ||
                            (sr.MatchingExpiresAtUtc.HasValue && sr.MatchingExpiresAtUtc.Value <= nowUtc);

        if (!sr.IsMatchingEligible || is24hExpired)
        {
            _logger.LogWarning("ServiceRequest {ServiceRequestId} matching window has expired or is not eligible.", serviceRequestId);
            return new MatchingExecutionResult
            {
                Success = false,
                Status = "MatchingExpired",
                Message = "Service request matching window has expired."
            };
        }

        int urgencyScore = sr.Urgency == ServiceRequestUrgency.Unknown ? 2 : (int)sr.Urgency;
        var objective = $"Category: {sr.Category}, Urgency: {urgencyScore}, Problem: {sr.ProblemSummary}, Location: {sr.LocationText}";

        var stopwatch = Stopwatch.StartNew();

        AgentWorkflow? agentWorkflow = null;
        AgentExecution? agentExecution = null;
        try
        {
            agentWorkflow = await _workflowService.CreateAsync(
                null,
                "ProviderMatching",
                serviceRequestId.ToString());

            await _workflowService.SetStatusAsync(agentWorkflow.Id, "Running");

            agentExecution = await _workflowService.StartExecutionAsync(
                agentWorkflow.Id,
                "ProviderMatchingAgent",
                new { ServiceRequestId = serviceRequestId });
        }
        catch (Exception wfEx)
        {
            _logger.LogWarning(wfEx, "Failed to initialize AgentWorkflow record for ServiceRequest {ServiceRequestId}", serviceRequestId);
        }

        // 3. Find all providers who have previously declined this request
        var declinedProviderIds = await _dbContext.MatchedCandidates
            .Where(m => m.MatchingExecution.ServiceRequestId == serviceRequestId 
                     && m.Status == MatchedCandidateStatus.Declined)
            .Select(m => m.ProviderId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // 3b. MaxActiveJobs dynamic capacity enforcement:
        // Exclude providers whose current active job count reaches their configured MaxActiveJobs limit.
        var busyProviderIds = await _dbContext.ProviderProfiles
            .Where(p => _dbContext.MatchedCandidates.Count(m =>
                m.ProviderId == p.Id &&
                (m.Status == MatchedCandidateStatus.Accepted ||
                 (m.Status == MatchedCandidateStatus.Recommended &&
                  m.MatchingExecution != null &&
                  m.MatchingExecution.ServiceRequestId != serviceRequestId &&
                  m.MatchingExecution.Status == MatchingExecutionStatus.Completed))
            ) >= p.MaxActiveJobs)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        // 4. Fetch live active, verified providers directly from database matching request category
        var targetCat = (sr.Category ?? string.Empty).Trim().ToLowerInvariant();
        bool isVehicle = targetCat.Contains("vehicle");
        bool isPlumbing = targetCat.Contains("plumb");
        bool isElectrical = targetCat.Contains("electr");
        bool isAppliance = targetCat.Contains("appliance");

        var eligibleQuery = _dbContext.ProviderProfiles
            .Include(p => p.Locations)
            .Include(p => p.Skills)
            .Where(p => p.IsOnline && p.VerificationStatus == ProviderVerificationStatus.Verified);

        // Exclude providers who already declined this request
        if (declinedProviderIds.Count > 0)
        {
            eligibleQuery = eligibleQuery.Where(p => !declinedProviderIds.Contains(p.Id));
        }

        // Enforce MaxActiveJobs = 1: Exclude providers currently busy with another active job
        if (busyProviderIds.Count > 0)
        {
            _logger.LogInformation("Enforcing MaxActiveJobs=1 for ServiceRequest {ServiceRequestId}. Excluded {Count} busy provider(s): {ProviderIds}",
                serviceRequestId, busyProviderIds.Count, string.Join(", ", busyProviderIds));
            eligibleQuery = eligibleQuery.Where(p => !busyProviderIds.Contains(p.Id));
        }

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

        if (eligibleProviders.Count == 0)
        {
            var noProviderMsg = declinedProviderIds.Count > 0
                ? "All eligible providers have declined this request."
                : (busyProviderIds.Count > 0
                    ? "Eligible providers found but all are currently busy with active jobs (MaxActiveJobs=1)."
                    : "No eligible providers found within their operational radius.");

            _logger.LogInformation("No eligible providers found for ServiceRequest {ServiceRequestId}: {Message}", serviceRequestId, noProviderMsg);

            var failedExecution = new MatchingExecution
            {
                ServiceRequestId = serviceRequestId,
                Status = MatchingExecutionStatus.Failed,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };
            _dbContext.MatchingExecutions.Add(failedExecution);
            await _dbContext.SaveChangesAsync(cancellationToken);

            stopwatch.Stop();
            if (agentWorkflow != null && agentExecution != null)
            {
                try
                {
                    await _workflowService.CompleteExecutionAsync(agentExecution.Id, true, new { message = noProviderMsg });
                    await _workflowService.SetStatusAsync(agentWorkflow.Id, "Completed");
                    await _monitoringService.RecordAsync(
                        agentWorkflow.Id,
                        agentExecution.Id,
                        "ProviderMatchingAgent",
                        "Completed",
                        stopwatch.ElapsedMilliseconds,
                        0);
                }
                catch (Exception monEx)
                {
                    _logger.LogWarning(monEx, "Failed to record monitoring metrics for ServiceRequest {ServiceRequestId}", serviceRequestId);
                }
            }

            return new MatchingExecutionResult
            {
                Success = false,
                Status = "No_Eligible_Providers",
                Message = noProviderMsg,
                MatchingExecutionId = failedExecution.Id
            };
        }

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

                stopwatch.Stop();
                if (agentWorkflow != null && agentExecution != null)
                {
                    try
                    {
                        await _workflowService.CompleteExecutionAsync(agentExecution.Id, true, new { message = emptyMsg });
                        await _workflowService.SetStatusAsync(agentWorkflow.Id, "Completed");
                        await _monitoringService.RecordAsync(
                            agentWorkflow.Id,
                            agentExecution.Id,
                            "ProviderMatchingAgent",
                            "Completed",
                            stopwatch.ElapsedMilliseconds,
                            eligibleProviders.Count);
                    }
                    catch (Exception monEx)
                    {
                        _logger.LogWarning(monEx, "Failed to record monitoring metrics for ServiceRequest {ServiceRequestId}", serviceRequestId);
                    }
                }

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

            // Auto-approval is strictly restricted to immediate cascade dispatches (when a provider declines on their device).
            // Any request matched from the queue (e.g. when a provider logs in later), or initial request, MUST pause for Admin Match Approval.
            var isAutoApproved = autoApprove;

            execution.Status = isAutoApproved
                ? MatchingExecutionStatus.Completed
                : MatchingExecutionStatus.PendingApproval;

            if (isAutoApproved)
            {
                execution.CompletedAt = DateTime.UtcNow;
            }

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

            stopwatch.Stop();
            if (agentWorkflow != null && agentExecution != null)
            {
                try
                {
                    await _workflowService.CompleteExecutionAsync(agentExecution.Id, true, new { threadId = matchResponse.ThreadId, providerId = providerGuid });
                    await _workflowService.SetStatusAsync(agentWorkflow.Id, "Completed");
                    await _monitoringService.RecordAsync(
                        agentWorkflow.Id,
                        agentExecution.Id,
                        "ProviderMatchingAgent",
                        "Completed",
                        stopwatch.ElapsedMilliseconds,
                        Math.Max(1, eligibleProviders.Count));
                }
                catch (Exception monEx)
                {
                    _logger.LogWarning(monEx, "Failed to record monitoring metrics for ServiceRequest {ServiceRequestId}", serviceRequestId);
                }
            }

            if (isAutoApproved)
            {
                _logger.LogInformation("ServiceRequest {ServiceRequestId} automatically dispatched to provider {ProviderId} (immediate cascade active).", serviceRequestId, providerGuid);
            }
            else
            {
                _logger.LogInformation("ServiceRequest {ServiceRequestId} successfully paused at LangGraph approval gate with thread {ThreadId}.", serviceRequestId, matchResponse.ThreadId);
            }

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

            stopwatch.Stop();
            if (agentWorkflow != null && agentExecution != null)
            {
                try
                {
                    await _workflowService.CompleteExecutionAsync(agentExecution.Id, false, new { error = httpEx.Message });
                    await _workflowService.SetStatusAsync(agentWorkflow.Id, "Failed");
                    await _monitoringService.RecordAsync(
                        agentWorkflow.Id,
                        agentExecution.Id,
                        "ProviderMatchingAgent",
                        "Failed",
                        stopwatch.ElapsedMilliseconds,
                        0);
                }
                catch { }
            }

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

            stopwatch.Stop();
            if (agentWorkflow != null && agentExecution != null)
            {
                try
                {
                    await _workflowService.CompleteExecutionAsync(agentExecution.Id, false, new { error = "Timeout" });
                    await _workflowService.SetStatusAsync(agentWorkflow.Id, "Failed");
                    await _monitoringService.RecordAsync(
                        agentWorkflow.Id,
                        agentExecution.Id,
                        "ProviderMatchingAgent",
                        "Failed",
                        stopwatch.ElapsedMilliseconds,
                        0);
                }
                catch { }
            }

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

            stopwatch.Stop();
            if (agentWorkflow != null && agentExecution != null)
            {
                try
                {
                    await _workflowService.CompleteExecutionAsync(agentExecution.Id, false, new { error = ex.Message });
                    await _workflowService.SetStatusAsync(agentWorkflow.Id, "Failed");
                    await _monitoringService.RecordAsync(
                        agentWorkflow.Id,
                        agentExecution.Id,
                        "ProviderMatchingAgent",
                        "Failed",
                        stopwatch.ElapsedMilliseconds,
                        0);
                }
                catch { }
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
