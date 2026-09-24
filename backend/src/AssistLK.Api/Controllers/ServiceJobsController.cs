using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using AssistLK.Infrastructure.Data;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;

namespace AssistLK.Api.Controllers
{
    [ApiController]
    [Route("api/service-jobs")]
    public class ServiceJobsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly AssistLKDbContext _serviceRequestContext;
        private readonly HttpClient _httpClient;

        public ServiceJobsController(
            ApplicationDbContext context,
            AssistLKDbContext serviceRequestContext,
            HttpClient httpClient)
        {
            _context = context;
            _serviceRequestContext = serviceRequestContext;
            _httpClient = httpClient;
        }

        [HttpPut("{id:guid}/status")]
        public async Task<IActionResult> UpdateJobStatus(Guid id, [FromBody] StatusUpdateRequest request)
        {
            try
            {
                var job = await _context.ServiceJobs.FindAsync(id);
                if (job == null)
                    return NotFound(new { message = "Job non-existent" });

                if (!Enum.TryParse<ServiceJobStatus>(request.NewStatus, true, out var newStatusEnum))
                {
                    return BadRequest(new { message = $"Invalid status value: {request.NewStatus}" });
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

                _context.ServiceStatusHistories.Add(historyRecord);

                ServiceRequest? serviceRequest = null;
                if (job.ServiceRequestId.HasValue)
                {
                    serviceRequest = await _serviceRequestContext.ServiceRequests
                        .FindAsync(job.ServiceRequestId.Value);

                    if (serviceRequest != null)
                    {
                        serviceRequest.Status = MapServiceRequestStatus(
                            newStatusEnum,
                            serviceRequest.Status);
                    }
                }

                await _context.SaveChangesAsync();

                if (serviceRequest != null)
                {
                    await _serviceRequestContext.SaveChangesAsync();
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

        [HttpPost("seed-from-request/{serviceRequestId:guid}")]
        public async Task<IActionResult> SeedFromRequest(Guid serviceRequestId)
        {
            try
            {
                var existingJob = await _context.ServiceJobs
                    .FirstOrDefaultAsync(j => j.ServiceRequestId == serviceRequestId);

                if (existingJob != null)
                {
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

                _context.ServiceJobs.Add(newJob);
                _context.ServiceStatusHistories.Add(initialHistory);
                await _context.SaveChangesAsync();

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
        public async Task<IActionResult> SubmitFeedback(Guid id, [FromBody] SubmitFeedbackDto dto)
        {
            try
            {
                var job = await _context.ServiceJobs.FindAsync(id);
                if (job == null)
                {
                    return NotFound(new { Message = "ServiceJob sapadla nahi." });
                }

                var feedbackAlreadyExists = await _context.Feedbacks
                    .AnyAsync(feedback => feedback.ServiceJobId == id);
                if (feedbackAlreadyExists)
                {
                    return BadRequest(new
                    {
                        message = "Feedback has already been submitted for this service job."
                    });
                }

                var sentimentResult = new SentimentResponse { IsNegative = false, Sentiment = "NEUTRAL" };

                try
                {
                    var response = await _httpClient.PostAsJsonAsync("http://localhost:8000/agent/analyze-sentiment", new { comment = dto.Comment });
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
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

                var feedback = new Feedback
                {
                    Id = Guid.NewGuid(),
                    ServiceJobId = id,
                    CustomerId = dto.CustomerId,
                    Rating = dto.Rating,
                    Comment = dto.Comment,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Feedbacks.Add(feedback);

                bool autoEscalated = false;
                Guid? complaintId = null;

                if (sentimentResult.IsNegative || dto.Rating <= 2)
                {
                    var complaint = new Complaint
                    {
                        Id = Guid.NewGuid(),
                        ServiceJobId = id,
                        CustomerId = dto.CustomerId,
                        Type = "Negative Feedback Auto-Escalation",
                        Description = $"AI Sentiment: {sentimentResult.Sentiment}. Comment: {dto.Comment}",
                        Status = "Open",
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Complaints.Add(complaint);
                    autoEscalated = true;
                    complaintId = complaint.Id;
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    FeedbackId = feedback.Id,
                    AutoEscalatedToComplaint = autoEscalated,
                    ComplaintId = complaintId,
                    Sentiment = sentimentResult.Sentiment,
                    Message = autoEscalated
                        ? "Your feedback has been saved, and a support ticket has been automatically created to address your concerns."
                        : "Thank you for your valuable feedback!"
                });
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
        public string ChangedByUserId { get; set; } = string.Empty;
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