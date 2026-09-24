using AssistLK.Infrastructure.Data;
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

    public ReportsController(ApplicationDbContext context)
    {
        _context = context;
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
                Type = complaint.Type,
                Subject = complaint.Subject,
                Description = complaint.Description,
                Status = complaint.Status,
                CreatedAt = complaint.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(complaints);
    }
}

public sealed class ComplaintResponse
{
    public Guid Id { get; init; }
    public Guid ServiceJobId { get; init; }
    public Guid CustomerId { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
