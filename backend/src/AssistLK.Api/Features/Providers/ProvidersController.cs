using AssistLK.Application.Interfaces;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AssistLK.Api.Features.Providers;

[ApiController]
[Route("api/providers")]
public class ProvidersController : ControllerBase
{
    private readonly IAgentWorkflowDbContext _dbContext;
    private readonly ILogger<ProvidersController> _logger;

    public ProvidersController(IAgentWorkflowDbContext dbContext, ILogger<ProvidersController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Flutter Mobile: Toggle provider online/offline status and update GPS coordinates.
    /// </summary>
    [HttpPut("availability")]
    [Authorize(Roles = "Provider")]
    public async Task<IActionResult> UpdateAvailability([FromBody] UpdateAvailabilityRequest request, CancellationToken cancellationToken)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized("Invalid user claim.");
        }

        var profile = await _dbContext.ProviderProfiles
            .Include(p => p.Locations)
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile == null)
        {
            return NotFound("Provider profile not found.");
        }

        profile.IsOnline = request.IsOnline;
        profile.UpdatedAt = DateTime.UtcNow;

        if (request.Latitude.HasValue && request.Longitude.HasValue)
        {
            var location = profile.Locations.FirstOrDefault();
            if (location == null)
            {
                location = new ProviderLocation
                {
                    Id = Guid.NewGuid(),
                    ProviderId = profile.Id,
                    Latitude = request.Latitude.Value,
                    Longitude = request.Longitude.Value,
                    OperatingRadiusKm = request.OperatingRadiusKm ?? 15.0m,
                    LastLocationUpdate = DateTime.UtcNow
                };
                _dbContext.ProviderLocations.Add(location);
            }
            else
            {
                location.Latitude = request.Latitude.Value;
                location.Longitude = request.Longitude.Value;
                if (request.OperatingRadiusKm.HasValue)
                {
                    location.OperatingRadiusKm = request.OperatingRadiusKm.Value;
                }
                location.LastLocationUpdate = DateTime.UtcNow;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Availability updated successfully.", isOnline = profile.IsOnline });
    }

    /// <summary>
    /// React Web Admin: Fetch providers awaiting credential verification.
    /// </summary>
    [HttpGet("verification-queue")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetVerificationQueue(CancellationToken cancellationToken)
    {
        var pendingProviders = await _dbContext.ProviderProfiles
            .Include(p => p.Skills)
            .Include(p => p.User)
            .Where(p => p.VerificationStatus == ProviderVerificationStatus.Pending)
            .Select(p => new
            {
                ProviderId = p.Id,
                FullName = p.User.FullName,
                BusinessName = p.BusinessName,
                Email = p.User.Email,
                PhoneNumber = p.User.PhoneNumber,
                CreatedAt = p.CreatedAt,
                Skills = p.Skills.Select(s => new { s.SkillName, s.Category, s.CertificationUrl })
            })
            .ToListAsync(cancellationToken);

        return Ok(pendingProviders);
    }
}

public class UpdateAvailabilityRequest
{
    public bool IsOnline { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? OperatingRadiusKm { get; set; }
}