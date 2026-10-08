using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Claims;
using AssistLK.Application.Attachments;
using AssistLK.Application.Interfaces;
using AssistLK.Application.Services;
using AssistLK.Infrastructure.Data;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AssistLK.Api.Controllers
{
    [ApiController]
    [Route("api/service-jobs")]
    public class ServiceJobsController : ControllerBase
    {
        private readonly AssistLKDbContext _assistLkDbContext;
        private readonly HttpClient _httpClient;
        private readonly IProofOfWorkStorage _proofOfWorkStorage;
        private readonly IHubContext<TrackingHub> _trackingHubContext;
        private readonly FeedbackApplicationService _feedbackApplicationService;
        private readonly AgentWorkflowService _agentWorkflowService;
        private readonly AgentMonitoringService _agentMonitoringService;
        private readonly ILogger<ServiceJobsController> _logger;

        public ServiceJobsController(
            AssistLKDbContext context,
            HttpClient httpClient,
            IProofOfWorkStorage proofOfWorkStorage,
            IHubContext<TrackingHub> trackingHubContext,
            FeedbackApplicationService feedbackApplicationService,
            AgentWorkflowService agentWorkflowService,
            AgentMonitoringService agentMonitoringService,
            ILogger<ServiceJobsController> logger)
        {
            _assistLkDbContext = context;
            _httpClient = httpClient;
            _proofOfWorkStorage = proofOfWorkStorage;
            _trackingHubContext = trackingHubContext;
            _feedbackApplicationService = feedbackApplicationService;
            _agentWorkflowService = agentWorkflowService;
            _agentMonitoringService = agentMonitoringService;
            _logger = logger;
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetServiceJobs(
            [FromQuery] string? status = null,
            CancellationToken cancellationToken = default)
        {
            var query = _assistLkDbContext.ServiceJobs
                .AsNoTracking()
                .Include(job => job.CompletionRecord)
                .Include(job => job.Feedback)
                .Include(job => job.Complaints)
                .OrderByDescending(job => job.CreatedAt)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) &&
                Enum.TryParse<ServiceJobStatus>(status, true, out var parsedStatus))
            {
                query = query.Where(job => job.Status == parsedStatus);
            }

            var jobs = await query
                .Select(job => new
                {
                    id = job.Id,
                    serviceRequestId = job.ServiceRequestId,
                    bookingId = job.BookingId,
                    providerId = job.ProviderId,
                    status = job.Status.ToString(),
                    startedAt = job.StartedAt,
                    completedAt = job.CompletedAt,
                    createdAt = job.CreatedAt,
                    completion = job.CompletionRecord == null
                        ? null
                        : new
                        {
                            proofOfWorkImageUrl = job.CompletionRecord.ProofOfWorkImageUrl,
                            summaryNotes = job.CompletionRecord.WorkSummary,
                            completedAt = job.CompletionRecord.CompletedAt
                        },
                    feedback = job.Feedback == null
                        ? null
                        : new
                        {
                            rating = job.Feedback.Rating,
                            comment = job.Feedback.Comment
                        },
                    complaintCount = job.Complaints.Count
                })
                .ToListAsync(cancellationToken);

            return Ok(jobs);
        }
                [HttpPost("resolve-from-match")]
        [Authorize(Roles = "Provider")]
        public async Task<IActionResult> ResolveJobFromMatch(
            [FromBody] ResolveJobFromMatchRequest request,
            CancellationToken cancellationToken = default)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var profile = await _assistLkDbContext.ProviderProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
            if (profile == null)
                return NotFound(new { message = "Provider profile not found." });

            var match = await _assistLkDbContext.MatchedCandidates
                .AsNoTracking()
                .Include(m => m.MatchingExecution)
                .FirstOrDefaultAsync(
                    m => m.Id == request.MatchId
                      && m.ProviderId == profile.Id
                      && m.Status == MatchedCandidateStatus.Accepted,
                    cancellationToken);

            if (match?.MatchingExecution == null)
                return NotFound(new { message = "No accepted match found for this provider." });

            var serviceRequestId = match.MatchingExecution.ServiceRequestId;

            var job = await _assistLkDbContext.ServiceJobs
                .Where(j => j.ServiceRequestId == serviceRequestId
                         && j.ProviderId == profile.Id
                         && j.Status != ServiceJobStatus.Cancelled)
                .OrderByDescending(j => j.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (job == null)
            {
                job = new ServiceJob
                {
                    ServiceRequestId = serviceRequestId,
                    ProviderId = profile.Id,
                    Status = ServiceJobStatus.Assigned,
                    CreatedAt = DateTime.UtcNow
                };
                _assistLkDbContext.ServiceJobs.Add(job);
                await _assistLkDbContext.SaveChangesAsync(cancellationToken);
            }

            return Ok(new { serviceJobId = job.Id, status = job.Status.ToString() });
        }

        [HttpPut("{id:guid}/status")]
        [Authorize]
        public async Task<IActionResult> UpdateJobStatus(Guid id, [FromBody] StatusUpdateRequest request)
        {
            try
            {
                var job = await _assistLkDbContext.ServiceJobs.FindAsync(id);
                if (job == null)
                    return NotFound(new { message = "Job non-existent" });

                if (!Enum.TryParse<ServiceJobStatus>(request.NewStatus, true, out var newStatusEnum))
                {
                    return BadRequest(new { message = $"Invalid status value: {request.NewStatus}" });
                }

                var isAdmin = User.IsInRole("Admin");
                if (!isAdmin)
                {
                    if (!User.IsInRole("Provider") ||
                        (newStatusEnum != ServiceJobStatus.InProgress &&
                         newStatusEnum != ServiceJobStatus.Completed &&
                         newStatusEnum != ServiceJobStatus.OnTheWay &&
                         newStatusEnum != ServiceJobStatus.Arrived))
                    {
                        return Forbid();
                    }

                    var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (!Guid.TryParse(userIdClaim, out var userId) || !job.ProviderId.HasValue)
                    {
                        return Forbid();
                    }

                    var ownsJob = await _assistLkDbContext.ProviderProfiles
                        .AnyAsync(
                            profile => profile.Id == job.ProviderId.Value && profile.UserId == userId);

                    if (!ownsJob)
                    {
                        return Forbid();
                    }
                }

                var agentPayload = new
                {
                    job_id = id.ToString(),
                    current_status = job.Status.ToString(),
                    target_status = request.NewStatus,
                    elapsed_minutes = request.TimeElapsedMinutes,
                    note = request.Notes ?? ""
                };

                var validation = await ExecuteValidationAsync(
                    agentPayload,
                    HttpContext.RequestAborted);
                if (!validation.ServiceAvailable)
                    return StatusCode(500, new { message = "AI Validation Service unavailable" });

                var agentResult = validation.Result;

                if (agentResult?.Status == "INVALID")
                {
                    return BadRequest(new { message = "Status transition blocked by AI Guardrail", reason = agentResult.Reason });
                }

                var oldStatusEnum = job.Status;
                job.Status = newStatusEnum;

                var historyRecord = new ServiceStatusHistory
                {
                    Id = Guid.NewGuid(),
                    ServiceJobId = id,
                    OldStatus = oldStatusEnum,
                    NewStatus = newStatusEnum,
                    Note = $"[AI Validation: {agentResult?.Status}] {request.Notes}",
                    ChangedAt = DateTime.UtcNow
                };

                _assistLkDbContext.ServiceStatusHistories.Add(historyRecord);

                if (newStatusEnum == ServiceJobStatus.Completed)
                {
                    var completion = await _assistLkDbContext.CompletionRecords
                        .FirstOrDefaultAsync(record => record.ServiceJobId == id);

                    if (completion == null)
                    {
                        completion = new CompletionRecord
                        {
                            Id = Guid.NewGuid(),
                            ServiceJobId = id,
                            CompletedAt = DateTime.UtcNow
                        };
                        _assistLkDbContext.CompletionRecords.Add(completion);
                    }

                    var completedAt = DateTime.UtcNow;
                    job.CompletedAt = completedAt;
                    completion.WorkSummary = request.Notes ?? "Work completed";
                    completion.ProofOfWorkImageUrl = request.ProofOfWorkImageUrl;
                    completion.AdditionalCost = 0;
                    completion.CompletedAt = completedAt;
                }

                ServiceRequest? serviceRequest = null;
                if (job.ServiceRequestId.HasValue)
                {
                    serviceRequest = await _assistLkDbContext.ServiceRequests
                        .FindAsync(job.ServiceRequestId.Value);

                    if (serviceRequest != null)
                    {
                        serviceRequest.Status = MapServiceRequestStatus(
                            newStatusEnum,
                            serviceRequest.Status);
                    }
                }

                await _assistLkDbContext.SaveChangesAsync();

                if (serviceRequest != null)
                {
                    await _assistLkDbContext.SaveChangesAsync();
                }

                if (newStatusEnum == ServiceJobStatus.Arrived)
                {
                    await _trackingHubContext.Clients
                        .Group($"Job_{id}")
                        .SendAsync(
                            "TrackingStopped",
                            new
                            {
                                JobId = id,
                                Status = ServiceJobStatus.Arrived.ToString()
                            });
                }

                return Ok(new { message = "Status updated successfully", current_status = job.Status.ToString(), ai_validation = agentResult });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while updating job status", error = ex.Message });
            }
        }

        private async Task<(AgentValidationResponse? Result, bool ServiceAvailable)> ExecuteValidationAsync(
            object payload,
            CancellationToken cancellationToken)
        {
            const string agentName = "TrackingValidationAgent";
            var workflow = await _agentWorkflowService.CreateAsync(
                null,
                "TrackingValidation",
                JsonSerializer.Serialize(payload));
            await _agentWorkflowService.SetStatusAsync(workflow.Id, "Running");
            var execution = await _agentWorkflowService.StartExecutionAsync(
                workflow.Id,
                agentName,
                payload);
            var stopwatch = Stopwatch.StartNew();
            AgentValidationResponse? result = null;
            var success = false;
            object? output = null;

            try
            {
                using var response = await _httpClient.PostAsJsonAsync(
                    "http://localhost:8003/agent/validate",
                    payload,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Tracking validation agent returned HTTP {StatusCode}; bypassing validation for job {JobId}.",
                        (int)response.StatusCode,
                        payload.GetType().GetProperty("job_id")?.GetValue(payload));
                    output = new { statusCode = (int)response.StatusCode };
                }
                else
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    result = JsonSerializer.Deserialize<AgentValidationResponse>(
                        content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    success = result?.Status != "INVALID";
                    output = result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Tracking validation agent is unavailable; bypassing validation for the status update.");
            }

            await CompleteValidationExecutionAsync(
                workflow.Id,
                execution.Id,
                agentName,
                success,
                output,
                stopwatch);
            return (result, true);
        }

        private async Task CompleteValidationExecutionAsync(
            Guid workflowId,
            Guid executionId,
            string agentName,
            bool success,
            object? output,
            Stopwatch stopwatch)
        {
            stopwatch.Stop();
            await _agentWorkflowService.CompleteExecutionAsync(executionId, success, output);
            await _agentWorkflowService.SetStatusAsync(
                workflowId,
                success ? "Completed" : "Failed");
            try
            {
                await _agentMonitoringService.RecordAsync(
                    workflowId,
                    executionId,
                    agentName,
                    success ? "Completed" : "Failed",
                    stopwatch.ElapsedMilliseconds,
                    0);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to persist telemetry for {AgentName} execution {ExecutionId} in workflow {WorkflowId}.",
                    agentName,
                    executionId,
                    workflowId);
                throw;
            }
        }

        private static ServiceRequestStatus MapServiceRequestStatus(
            ServiceJobStatus jobStatus,
            ServiceRequestStatus currentStatus)
        {
            return jobStatus switch
            {
                ServiceJobStatus.Cancelled => ServiceRequestStatus.Cancelled,
                _ => currentStatus
            };
        }

        [HttpPut("{id:guid}/complete")]
        [Authorize]
        [RequestSizeLimit(10 * 1024 * 1024)]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> CompleteJob(
            Guid id,
            [FromForm] CompleteJobForm form,
            [FromForm] IFormFile? proofOfWorkImage,
            CancellationToken cancellationToken)
        {
            try
            {
                var job = await _assistLkDbContext.ServiceJobs
                    .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
                if (job == null)
                {
                    return NotFound(new { message = "Job non-existent" });
                }

                if (!await CanCompleteJobAsync(job, cancellationToken))
                {
                    return Forbid();
                }

                if (proofOfWorkImage == null || proofOfWorkImage.Length == 0)
                {
                    return BadRequest(new { message = "A proof-of-work image is required to complete the job." });
                }

                if (proofOfWorkImage != null && proofOfWorkImage.Length > 10 * 1024 * 1024)
                {
                    return BadRequest(new { message = "Proof of work image must be 10 MB or smaller." });
                }

                if (proofOfWorkImage != null &&
                    !new[] { "image/jpeg", "image/png", "image/webp" }
                        .Contains(proofOfWorkImage.ContentType, StringComparer.OrdinalIgnoreCase))
                {
                    return BadRequest(new { message = "Proof of work must be a JPEG, PNG, or WebP image." });
                }

                var validation = await ExecuteValidationAsync(
                    new
                    {
                        job_id = id.ToString(),
                        current_status = job.Status.ToString(),
                        target_status = ServiceJobStatus.Completed.ToString(),
                        elapsed_minutes = form.TimeElapsedMinutes,
                        note = form.Notes ?? string.Empty
                    },
                    cancellationToken);

                if (!validation.ServiceAvailable)
                {
                    return StatusCode(500, new { message = "AI Validation Service unavailable" });
                }

                var validationResult = validation.Result;
                var rapidCompletionFlagged = string.Equals(
                    validationResult?.Status,
                    "SUSPICIOUS",
                    StringComparison.OrdinalIgnoreCase);

                if (validationResult?.Status == "INVALID")
                {
                    return BadRequest(new
                    {
                        message = "Status transition blocked by AI Guardrail",
                        reason = validationResult.Reason
                    });
                }

                string? proofOfWorkImageUrl;
                await using (var imageStream = proofOfWorkImage!.OpenReadStream())
                {
                    proofOfWorkImageUrl = await _proofOfWorkStorage.UploadAsync(
                        imageStream,
                        proofOfWorkImage.FileName,
                        proofOfWorkImage.ContentType,
                        id,
                        cancellationToken);
                }

                var oldStatus = job.Status;
                job.Status = ServiceJobStatus.Completed;
                job.CompletedAt = DateTime.UtcNow;
                _assistLkDbContext.ServiceStatusHistories.Add(new ServiceStatusHistory
                {
                    Id = Guid.NewGuid(),
                    ServiceJobId = id,
                    OldStatus = oldStatus,
                    NewStatus = ServiceJobStatus.Completed,
                    Note = $"[AI Validation: {validationResult?.Status}]" +
                        (rapidCompletionFlagged ? " [Rapid Completion Flagged]" : string.Empty) +
                        $" {form.Notes}",
                    ChangedAt = DateTime.UtcNow
                });

                var completion = await _assistLkDbContext.CompletionRecords
                    .FirstOrDefaultAsync(record => record.ServiceJobId == id, cancellationToken);
                if (completion == null)
                {
                    completion = new CompletionRecord
                    {
                        Id = Guid.NewGuid(),
                        ServiceJobId = id
                    };
                    _assistLkDbContext.CompletionRecords.Add(completion);
                }

                completion.WorkSummary = string.IsNullOrWhiteSpace(form.Notes)
                    ? "Work completed"
                    : form.Notes;
                completion.ProofOfWorkImageUrl = proofOfWorkImageUrl;
                completion.AdditionalCost = 0;
                completion.CompletedAt = DateTime.UtcNow;

                await _assistLkDbContext.SaveChangesAsync(cancellationToken);

                return Ok(new
                {
                    message = "Job completed successfully.",
                    current_status = job.Status.ToString(),
                    proofOfWorkImageUrl,
                    ai_validation = validationResult,
                    rapid_completion_flagged = rapidCompletionFlagged
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while completing the job", error = ex.Message });
            }
        }

        private async Task<bool> CanCompleteJobAsync(
            ServiceJob job,
            CancellationToken cancellationToken)
        {
            if (User.IsInRole("Admin"))
            {
                return true;
            }

            if (!User.IsInRole("Provider") || !job.ProviderId.HasValue)
            {
                return false;
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(userIdClaim, out var userId) &&
                await _assistLkDbContext.ProviderProfiles.AnyAsync(
                    profile => profile.Id == job.ProviderId.Value && profile.UserId == userId,
                    cancellationToken);
        }

        [HttpPost("{id:guid}/feedback")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> SubmitFeedback(
            Guid id,
            [FromBody] SubmitFeedbackDto dto,
            CancellationToken cancellationToken)
        {
            try
            {
                var job = await _assistLkDbContext.ServiceJobs
                    .FindAsync([id], cancellationToken);
                if (job == null)
                {
                    return NotFound(new { Message = "Service job was not found." });
                }

                var customerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!Guid.TryParse(customerIdClaim, out var customerId))
                {
                    return Unauthorized(new { message = "Customer identity is missing or invalid." });
                }

                var ownsJob = job.ServiceRequestId.HasValue &&
                    await _assistLkDbContext.ServiceRequests.AnyAsync(
                        request => request.Id == job.ServiceRequestId.Value &&
                                   request.CustomerId == customerId,
                        cancellationToken);
                if (!ownsJob)
                {
                    return Forbid();
                }

                var sentimentResult = new SentimentResponse
                {
                    Sentiment = "NEUTRAL",
                    ShouldRouteToComplaint = false
                };

                try
                {
                    var response = await _httpClient.PostAsJsonAsync(
                        "http://localhost:8003/agent/analyze-sentiment",
                        new { feedback_text = dto.Comment },
                        cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync(cancellationToken);
                        var parsed = JsonSerializer.Deserialize<SentimentResponse>(content, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        if (parsed != null)
                        {
                            sentimentResult = parsed;
                        }
                    }
                }
                catch
                {
                    // Fallback to rating validation if sentiment agent is unreachable
                }

                var submissionResult = await _feedbackApplicationService.SubmitAsync(
                    id,
                    new SubmitFeedbackCommand(
                        customerId,
                        dto.Rating,
                        dto.Comment,
                        sentimentResult.ShouldRouteToComplaint || dto.Rating <= 2,
                        sentimentResult.Sentiment),
                    cancellationToken);

                return Ok(new
                {
                    submissionResult.FeedbackId,
                    submissionResult.AutoEscalatedToComplaint,
                    submissionResult.ComplaintId,
                    submissionResult.Sentiment,
                    submissionResult.Message
                });
            }
            catch (DuplicateFeedbackException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                return Conflict(new { message = "Feedback has already been submitted for this service job." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting feedback for service job {ServiceJobId}", id);
                return StatusCode(500, new { message = "Error submitting feedback", error = ex.Message });
            }
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException exception)
        {
            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                if (current is PostgresException postgresException &&
                    postgresException.SqlState == PostgresErrorCodes.UniqueViolation)
                {
                    return true;
                }
            }

            return false;
        }
    }
        public class ResolveJobFromMatchRequest
    {
        public Guid MatchId { get; set; }
    }

    public class StatusUpdateRequest
    {
        public string NewStatus { get; set; } = string.Empty;
        public double TimeElapsedMinutes { get; set; }
        public string? Notes { get; set; }
        public string? ProofOfWorkImageUrl { get; set; }
        public string ChangedByUserId { get; set; } = string.Empty;
    }

    public class CompleteJobForm
    {
        public string? Notes { get; set; }
        public double TimeElapsedMinutes { get; set; }
    }

    public class AgentValidationResponse
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("reason")]
        public string Reason { get; set; } = string.Empty;
    }

    public class SubmitFeedbackDto
    {
        public int Rating { get; set; }
        public string Comment { get; set; } = string.Empty;
    }

    public class SentimentResponse
    {
        [JsonPropertyName("sentiment")]
        public string Sentiment { get; set; } = string.Empty;

        [JsonPropertyName("should_route_to_complaint")]
        public bool ShouldRouteToComplaint { get; set; }

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;
    }
}