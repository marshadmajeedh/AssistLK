using AssistLK.Infrastructure.Data;
using AssistLK.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "Admin")]
public class ReportsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly AssistLKDbContext _assistLkContext;

    public ReportsController(
        ApplicationDbContext context,
        AssistLKDbContext assistLkContext)
    {
        _context = context;
        _assistLkContext = assistLkContext;
    }

    [HttpGet("complaints")]
    public async Task<ActionResult<IReadOnlyList<ComplaintResponse>>> GetComplaints(
        CancellationToken cancellationToken)
    {
        var complaints = await _context.Complaints
            .AsNoTracking()
            .OrderByDescending(complaint => complaint.CreatedAt)
            .Select(complaint => new ComplaintResponse
            {
                Id = complaint.Id,
                ServiceJobId = complaint.ServiceJobId,
                CustomerId = complaint.CustomerId,
                TicketId = complaint.Id,
                JobId = complaint.ServiceJobId,
                Type = complaint.Type,
                Subject = complaint.Subject,
                Description = complaint.Description,
                CustomerComment = complaint.CustomerComment,
                AiSentiment = complaint.AiSentiment,
                Status = complaint.Status,
                JobStatus = _context.ServiceJobs
                    .Where(job => job.Id == complaint.ServiceJobId)
                    .Select(job => job.Status.ToString())
                    .FirstOrDefault(),
                IsSuspicious = complaint.Type == "Negative Feedback Auto-Escalation",
                CreatedAt = complaint.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var customerIds = complaints
            .Select(complaint => complaint.CustomerId)
            .Distinct()
            .ToList();
        var customerNames = await _assistLkContext.Users
            .AsNoTracking()
            .Where(user => customerIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);

        foreach (var complaint in complaints)
        {
            complaint.CustomerName = customerNames.GetValueOrDefault(complaint.CustomerId);
        }

        return Ok(complaints);
    }

    [HttpPut("complaints/{complaintId:guid}/status")]
    public async Task<IActionResult> UpdateComplaintStatus(
        Guid complaintId,
        [FromBody] ComplaintStatusUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var supportedStatuses = new[] { "Approved", "Rejected", "Resolved" };
        if (!supportedStatuses.Contains(request.Status, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Only Approved, Rejected, or Resolved complaint statuses are supported." });
        }

        var complaint = await _context.Complaints
            .FirstOrDefaultAsync(item => item.Id == complaintId, cancellationToken);

        if (complaint == null)
        {
            return NotFound(new { message = "Complaint not found." });
        }

        complaint.Status = supportedStatuses.First(
            status => string.Equals(status, request.Status, StringComparison.OrdinalIgnoreCase));
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            complaint.Id,
            complaint.Status,
            Message = "Complaint resolved successfully."
        });
    }

    [HttpGet("suspicious-jobs")]
    public async Task<ActionResult<IReadOnlyList<SuspiciousJobResponse>>> GetSuspiciousJobs(
        CancellationToken cancellationToken)
    {
        var suspiciousJobs = await _context.ServiceJobs
            .AsNoTracking()
            .Where(job => job.StatusHistories.Any(history =>
                history.Note != null &&
                history.Note.Contains("Rapid Completion Flagged")))
            .Select(job => new SuspiciousJobResponse
            {
                ServiceJobId = job.Id,
                Status = job.Status.ToString(),
                FlaggedAt = job.StatusHistories
                    .Where(history =>
                        history.Note != null &&
                        history.Note.Contains("Rapid Completion Flagged"))
                    .OrderByDescending(history => history.ChangedAt)
                    .Select(history => history.ChangedAt)
                    .FirstOrDefault(),
                Reason = job.StatusHistories
                    .Where(history =>
                        history.Note != null &&
                        history.Note.Contains("Rapid Completion Flagged"))
                    .OrderByDescending(history => history.ChangedAt)
                    .Select(history => history.Note)
                    .FirstOrDefault() ?? "Rapid completion flagged for review."
            })
            .OrderByDescending(job => job.FlaggedAt)
            .ToListAsync(cancellationToken);

        return Ok(suspiciousJobs);
    }
}

public sealed class ComplaintResponse
{
    public Guid Id { get; init; }
    public Guid ServiceJobId { get; init; }
    public Guid CustomerId { get; init; }
    public Guid TicketId { get; init; }
    public Guid JobId { get; init; }
    public string? CustomerName { get; set; }
    public string Type { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? CustomerComment { get; init; }
    public string? AiSentiment { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? JobStatus { get; init; }
    public bool IsSuspicious { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class ComplaintStatusUpdateRequest
{
    public string Status { get; init; } = string.Empty;
    public string? Notes { get; init; }
}

public sealed class SuspiciousJobResponse
{
    public Guid ServiceJobId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime FlaggedAt { get; init; }
    public string Reason { get; init; } = string.Empty;
}
