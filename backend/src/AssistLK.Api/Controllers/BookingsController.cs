using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AssistLK.Application.Quotations;
using AssistLK.Application.Services.Quotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssistLK.Api.Controllers;

/// <summary>
/// Component 3 — Booking read endpoints.
/// The creation of a booking happens atomically during quotation approval
/// (see QuotationsController.Approve).
/// </summary>
[ApiController]
[Route("api/bookings")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly IQuotationService _quotationService;

    public BookingsController(IQuotationService quotationService)
    {
        _quotationService = quotationService;
    }

    /// <summary>
    /// Get a booking by its ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var booking = await _quotationService.GetBookingByIdAsync(id);
        if (booking is null) return NotFound();
        return Ok(booking);
    }

    /// <summary>
    /// Get the status history (audit trail) for a booking.
    /// </summary>
    [HttpGet("{id:int}/status-history")]
    [ProducesResponseType(typeof(IEnumerable<BookingStatusHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<BookingStatusHistoryDto>>> GetStatusHistory(
        int id,
        CancellationToken cancellationToken)
    {
        var history = await _quotationService.GetBookingStatusHistoryAsync(id);
        return Ok(history);
    }
}