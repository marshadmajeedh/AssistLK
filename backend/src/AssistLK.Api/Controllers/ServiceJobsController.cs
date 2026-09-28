using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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

namespace AssistLK.Api.Controllers
{
    [ApiController]
    [Route("api/service-jobs")]
    public class ServiceJobsController : ControllerBase
    {
        private readonly ApplicationDbContext _applicationDbContext;
        private readonly AssistLKDbContext _assistLkDbContext;
        private readonly HttpClient _httpClient;
        private readonly IProofOfWorkStorage _proofOfWorkStorage;
        private readonly IHubContext<TrackingHub> _trackingHubContext;
        private readonly FeedbackApplicationService _feedbackApplicationService;

        public ServiceJobsController(
            ApplicationDbContext context,
            AssistLKDbContext serviceRequestContext,
            HttpClient httpClient,
            IProofOfWorkStorage proofOfWorkStorage,
            IHubContext<TrackingHub> trackingHubContext,
            FeedbackApplicationService feedbackApplicationService)
        {
            _applicationDbContext = context;
            _assistLkDbContext = serviceRequestContext;
            _httpClient = httpClient;
            _proofOfWorkStorage = proofOfWorkStorage;
            _trackingHubContext = trackingHubContext;
            _feedbackApplicationService = feedbackApplicationService;
        }

        [HttpPut("{id:guid}/status")]
        [Authorize]
        public async Task<IActionResult> UpdateJobStatus(Guid id, [FromBody] StatusUpdateRequest request)
        {
            try
            {
                var job = await _applicationDbContext.ServiceJobs.FindAsync(id);
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

                var response = await _httpClient.PostAsJsonAsync("http://localhost:8000/agent/validate", agentPayload);
                if (!response.IsSuccessStatusCode)
                    return StatusCode(500, new { message = "AI Validation Service unavailable" });

                var content = await response.Content.ReadAsStringAsync();
                var agentResult = JsonSerializer.Deserialize<AgentValidationResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

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

                _applicationDbContext.ServiceStatusHistories.Add(historyRecord);

                if (newStatusEnum == ServiceJobStatus.Completed)
                {
                    var completion = await _applicationDbContext.CompletionRecords
                        .FirstOrDefaultAsync(record => record.ServiceJobId == id);

                    if (completion == null)
                    {
                        completion = new CompletionRecord
                        {
                            Id = Guid.NewGuid(),
                            ServiceJobId = id,
                            CompletedAt = DateTime.UtcNow
                        };
                        _applicationDbContext.CompletionRecords.Add(completion);
                    }

                    completion.WorkSummary = request.Notes ?? "Work completed";
                    completion.ProofOfWorkImageUrl = request.ProofOfWorkImageUrl;
                    completion.AdditionalCost = 0;
                    completion.CompletedAt = DateTime.UtcNow;
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

                await _applicationDbContext.SaveChangesAsync();

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

        private static ServiceRequestStatus MapServiceRequestStatus(
            ServiceJobStatus jobStatus,
            ServiceRequestStatus currentStatus)
        {
            return jobStatus switch
            {
                ServiceJobStatus.Completed => ServiceRequestStatus.Completed,
                ServiceJobStatus.Cancelled => ServiceRequestStatus.Cancelled,
                _ => currentStatus
            };
        }

        [HttpPut("{id:guid}/complete")]
        [Authorize]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> CompleteJob(
            Guid id,
            [FromForm] CompleteJobForm form,
            IFormFile? proofOfWorkImage,
            CancellationToken cancellationToken)
        {
            try
            {
                var job = await _applicationDbContext.ServiceJobs
                    .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
                if (job == null)
                {
                    return NotFound(new { message = "Job non-existent" });
                }

                if (!await CanCompleteJobAsync(job, cancellationToken))
                {
                    return Forbid();
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

                var validationResponse = await _httpClient.PostAsJsonAsync(
                    "http://localhost:8000/agent/validate",
                    new
                    {
                        job_id = id.ToString(),
                        current_status = job.Status.ToString(),
                        target_status = ServiceJobStatus.Completed.ToString(),
                        elapsed_minutes = form.TimeElapsedMinutes,
                        note = form.Notes ?? string.Empty
                    },
                    cancellationToken);

                if (!validationResponse.IsSuccessStatusCode)
                {
                    return StatusCode(500, new { message = "AI Validation Service unavailable" });
                }

                var validationContent = await validationResponse.Content.ReadAsStringAsync(cancellationToken);
                var validationResult = JsonSerializer.Deserialize<AgentValidationResponse>(
                    validationContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (validationResult?.Status == "INVALID")
                {
                    return BadRequest(new
                    {
                        message = "Status transition blocked by AI Guardrail",
                        reason = validationResult.Reason
                    });
                }

                string? proofOfWorkImageUrl = null;
                if (proofOfWorkImage != null)
                {
                    await using var imageStream = proofOfWorkImage.OpenReadStream();
                    proofOfWorkImageUrl = await _proofOfWorkStorage.UploadAsync(
                        imageStream,
                        proofOfWorkImage.FileName,
                        proofOfWorkImage.ContentType,
                        id,
                        cancellationToken);
                }

                var oldStatus = job.Status;
                job.Status = ServiceJobStatus.Completed;
                _applicationDbContext.ServiceStatusHistories.Add(new ServiceStatusHistory
                {
                    Id = Guid.NewGuid(),
                    ServiceJobId = id,
                    OldStatus = oldStatus,
                    NewStatus = ServiceJobStatus.Completed,
                    Note = $"[AI Validation: {validationResult?.Status}] {form.Notes}",
                    ChangedAt = DateTime.UtcNow
                });

                var completion = await _applicationDbContext.CompletionRecords
                    .FirstOrDefaultAsync(record => record.ServiceJobId == id, cancellationToken);
                if (completion == null)
                {
                    completion = new CompletionRecord
                    {
                        Id = Guid.NewGuid(),
                        ServiceJobId = id
                    };
                    _applicationDbContext.CompletionRecords.Add(completion);
                }

                completion.WorkSummary = string.IsNullOrWhiteSpace(form.Notes)
                    ? "Work completed"
                    : form.Notes;
                completion.ProofOfWorkImageUrl = proofOfWorkImageUrl;
                completion.AdditionalCost = 0;
                completion.CompletedAt = DateTime.UtcNow;

                if (job.ServiceRequestId.HasValue)
                {
                    var serviceRequest = await _assistLkDbContext.ServiceRequests
                        .FindAsync(new object[] { job.ServiceRequestId.Value }, cancellationToken);
                    if (serviceRequest != null)
                    {
                        serviceRequest.Status = ServiceRequestStatus.Completed;
                    }
                }

                await _applicationDbContext.SaveChangesAsync(cancellationToken);
                await _assistLkDbContext.SaveChangesAsync(cancellationToken);

                return Ok(new
                {
                    message = "Job completed successfully.",
                    current_status = job.Status.ToString(),
                    proofOfWorkImageUrl,
                    ai_validation = validationResult
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

        [HttpPost("seed-from-request/{serviceRequestId:guid}")]
        [Authorize(Roles = "Provider")]
        public async Task<IActionResult> SeedFromRequest(Guid serviceRequestId)
        {
            try
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!Guid.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized(new { message = "Authenticated provider identity is missing or invalid." });
                }

                var providerId = await _assistLkDbContext.ProviderProfiles
                    .Where(profile => profile.UserId == userId)
                    .Select(profile => (Guid?)profile.Id)
                    .FirstOrDefaultAsync();

                if (!providerId.HasValue)
                {
                    return Forbid();
                }

                var existingJob = await _applicationDbContext.ServiceJobs
                    .FirstOrDefaultAsync(j => j.ServiceRequestId == serviceRequestId);

                if (existingJob != null)
                {
                    if (!existingJob.ProviderId.HasValue)
                    {
                        existingJob.ProviderId = providerId.Value;
                        await _applicationDbContext.SaveChangesAsync();
                    }
                    else if (existingJob.ProviderId.Value != providerId.Value)
                    {
                        return Conflict(new
                        {
                            message = "This service request is already assigned to another provider."
                        });
                    }

                    return Ok(new
                    {
                        Message = "Job tya request sathi adhiich ahe.",
                        JobId = existingJob.Id,
                        Status = existingJob.Status.ToString()
                    });
                }

                var newJob = new ServiceJob
                {
                    Id = Guid.NewGuid(),
                    ServiceRequestId = serviceRequestId,
                    ProviderId = providerId.Value,
                    Status = ServiceJobStatus.Assigned,
                    CreatedAt = DateTime.UtcNow
                };

                var initialHistory = new ServiceStatusHistory
                {
                    Id = Guid.NewGuid(),
                    ServiceJobId = newJob.Id,
                    OldStatus = null,
                    NewStatus = ServiceJobStatus.Assigned,
                    Note = "Component 1 ServiceRequest kadun auto-seed kela.",
                    ChangedAt = DateTime.UtcNow
                };

                _applicationDbContext.ServiceJobs.Add(newJob);
                _applicationDbContext.ServiceStatusHistories.Add(initialHistory);
                await _applicationDbContext.SaveChangesAsync();

                return Ok(new
                {
                    JobId = newJob.Id,
                    ServiceRequestId = newJob.ServiceRequestId,
                    Status = newJob.Status.ToString(),
                    Message = "ServiceJob yashasviretya create zala."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error seeding job from request", error = ex.Message });
            }
        }

        [HttpPost("{id:guid}/feedback")]
        public async Task<IActionResult> SubmitFeedback(
            Guid id,
            [FromBody] SubmitFeedbackDto dto,
            CancellationToken cancellationToken)
        {
            try
            {
                var job = await _applicationDbContext.ServiceJobs
                    .FindAsync([id], cancellationToken);
                if (job == null)
                {
                    return NotFound(new { Message = "ServiceJob sapadla nahi." });
                }

                await using var transaction = await _applicationDbContext.Database.BeginTransactionAsync();
                var applicationConnection = _applicationDbContext.Database.GetDbConnection();
                if (!ReferenceEquals(_assistLkDbContext.Database.GetDbConnection(), applicationConnection))
                {
                    _assistLkDbContext.Database.SetDbConnection(applicationConnection);
                }
                await _assistLkDbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());

                var sentimentResult = new SentimentResponse { IsNegative = false, Sentiment = "NEUTRAL" };

                try
                {
                    var response = await _httpClient.PostAsJsonAsync(
                        "http://localhost:8000/agent/analyze-sentiment",
                        new { comment = dto.Comment },
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

                var result = await _feedbackApplicationService.SubmitAsync(
                    id,
                    new SubmitFeedbackCommand(
                        dto.CustomerId,
                        dto.Rating,
                        dto.Comment,
                        sentimentResult.IsNegative || dto.Rating <= 2,
                        sentimentResult.Sentiment),
                    cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return Ok(new
                {
                    result.FeedbackId,
                    result.AutoEscalatedToComplaint,
                    result.ComplaintId,
                    result.Sentiment,
                    result.Message
                });
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
                return StatusCode(500, new { message = "Error submitting feedback", error = ex.Message });
            }
        }
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
        public Guid CustomerId { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; } = string.Empty;
    }

    public class SentimentResponse
    {
        [JsonPropertyName("is_negative")]
        public bool IsNegative { get; set; }

        [JsonPropertyName("sentiment")]
        public string Sentiment { get; set; } = string.Empty;
    }
}