using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using AssistLK.Infrastructure.Data;
using AssistLK.Domain.Entities;

namespace AssistLK.Api.Controllers
{
    [ApiController]
    [Route("api/service-jobs")]
    public class ServiceJobsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly HttpClient _httpClient;

        public ServiceJobsController(ApplicationDbContext context, HttpClient httpClient)
        {
            _context = context;
            _httpClient = httpClient;
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateJobStatus(Guid id, [FromBody] StatusUpdateRequest request)
        {
            try
            {
                var job = await _context.ServiceJobs.FindAsync(id);
                if (job == null)
                    return NotFound(new { message = "Job non-existent" });

                // Convert string status into ServiceJobStatus Enum
                if (!Enum.TryParse<ServiceJobStatus>(request.NewStatus, true, out var newStatusEnum))
                {
                    return BadRequest(new { message = $"Invalid status value: {request.NewStatus}" });
                }

                // Python Agent Payload matched to ValidationRequest schema
                var agentPayload = new
                {
                    job_id = id.ToString(),
                    current_status = job.Status.ToString(),
                    target_status = request.NewStatus,
                    elapsed_minutes = request.TimeElapsedMinutes,
                    note = request.Notes ?? ""
                };

                // Step 3.1: Call Python Validation Agent
                var response = await _httpClient.PostAsJsonAsync("http://localhost:8000/agent/validate", agentPayload);
                if (!response.IsSuccessStatusCode)
                    return StatusCode(500, new { message = "AI Validation Service unavailable" });

                var content = await response.Content.ReadAsStringAsync();
                var agentResult = JsonSerializer.Deserialize<AgentValidationResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                // Step 3.2: Status Validation Check
                if (agentResult?.Status == "INVALID")
                {
                    return BadRequest(new { message = "Status transition blocked by AI Guardrail", reason = agentResult.Reason });
                }

                var oldStatusEnum = job.Status;
                job.Status = newStatusEnum;

                // Step 3.3: Insert into ServiceStatusHistory Table
                var historyRecord = new ServiceStatusHistory
                {
                    ServiceJobId = id,
                    OldStatus = oldStatusEnum,
                    NewStatus = newStatusEnum,
                    Note = $"[AI Validation: {agentResult?.Status}] {request.Notes}",
                    ChangedAt = DateTime.UtcNow
                };

                _context.ServiceStatusHistories.Add(historyRecord);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Status updated successfully", current_status = job.Status.ToString(), ai_validation = agentResult });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while updating job status", error = ex.Message });
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
}