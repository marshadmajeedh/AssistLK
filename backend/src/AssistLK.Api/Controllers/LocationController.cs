using AssistLK.Api.DTOs;
using AssistLK.Application.Common.Exceptions;
using AssistLK.Application.Interfaces;
using AssistLK.Application.Locations.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssistLK.Api.Controllers;

[ApiController]
[Route("api/location")]
[Authorize(Roles = "Customer")]
public class LocationController(ILocationGeocodingService geocoding) : ControllerBase
{
    [HttpPost("reverse-geocode")]
    public async Task<ActionResult<ReverseGeocodeResponse>> ReverseGeocode(
        ReverseGeocodeRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        try
        {
            var result = await geocoding.ReverseGeocodeAsync(request.Latitude!.Value, request.Longitude!.Value, cancellationToken);
            return result == null
                ? NotFound(new ErrorResponse { StatusCode = 404, Message = "No address found. Please enter the location manually." })
                : Ok(result);
        }
        catch (LocationGeocodingUnavailableException ex)
        {
            return StatusCode(503, new ErrorResponse { StatusCode = 503, Message = ex.Message });
        }
    }
}
