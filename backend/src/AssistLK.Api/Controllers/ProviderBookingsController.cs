using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AssistLK.Application.Services;

namespace AssistLK.Api.Controllers;

[ApiController]
[Route("api/provider/bookings")]
[Authorize(Roles = "Provider")]
public class ProviderBookingsController : ControllerBase
{
    private readonly IProviderBookingQueryService _query;

    public ProviderBookingsController(IProviderBookingQueryService query)
    {
        _query = query;
    }

    /// <summary>
    /// GET /api/provider/bookings?filter=upcoming|completed
    /// Returns the current provider's bookings for the Bookings tab.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMyBookings(
        [FromQuery] string? filter)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var providerId))
            return Unauthorized("Invalid provider identity.");

        var completed = string.Equals(
            filter, "completed", StringComparison.OrdinalIgnoreCase);

        var bookings = await _query.GetForProviderAsync(providerId, completed);
        return Ok(bookings);
    }
}