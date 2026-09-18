using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AssistLK.Application.Quotations;
using AssistLK.Application.Services.Quotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssistLK.Api.Controllers;

/// <summary>
/// Component 3 — Quotation &amp; Booking Management.
/// Handles quotation creation, submission for approval, and the
/// human-in-the-loop approve/reject workflow.
/// </summary>
[ApiController]
[Route("api/quotations")]
[Authorize]
public class QuotationsController : ControllerBase
{
    private readonly IQuotationService _quotationService;

    public QuotationsController(IQuotationService quotationService)
    {
        _quotationService = quotationService;
    }

    /// <summary>
    /// Provider creates a new quotation for a service request.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Provider")]
    [ProducesResponseType(typeof(QuotationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<QuotationDto>> Create(
        [FromBody] CreateQuotationDto dto,
        CancellationToken cancellationToken)
    {
        var providerUserId = GetCurrentUserId();
        var created = await _quotationService.CreateAsync(dto, providerUserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Get a quotation by its ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(QuotationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuotationDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var quotation = await _quotationService.GetByIdAsync(id);
        if (quotation is null) return NotFound();
        return Ok(quotation);
    }

    /// <summary>
    /// List all quotations associated with a service request.
    /// </summary>
    [HttpGet("by-request/{serviceRequestId:int}")]
    [ProducesResponseType(typeof(IEnumerable<QuotationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<QuotationDto>>> GetByServiceRequest(
        int serviceRequestId,
        CancellationToken cancellationToken)
    {
        var quotations = await _quotationService.GetByServiceRequestAsync(serviceRequestId);
        return Ok(quotations);
    }

    /// <summary>
    /// Provider submits a draft quotation for customer approval.
    /// This is the point where the Agentic AI workflow pauses —
    /// the AI cannot decide pricing.
    /// </summary>
    [HttpPost("{id:int}/send-for-approval")]
    [Authorize(Roles = "Provider")]
    [ProducesResponseType(typeof(QuotationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuotationDto>> SendForApproval(
        int id,
        CancellationToken cancellationToken)
    {
        var providerUserId = GetCurrentUserId();
        var updated = await _quotationService.SendForApprovalAsync(id, providerUserId);
        return Ok(updated);
    }

    /// <summary>
    /// Business-specific operation: Customer approves a quotation.
    /// Atomically transitions the quotation to Approved AND creates a Booking.
    /// </summary>
    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = "Customer")]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDto>> Approve(
        int id,
        [FromBody] ApproveQuotationDto dto,
        CancellationToken cancellationToken)
    {
        var customerUserId = GetCurrentUserId();
        var booking = await _quotationService.ApproveAsync(id, dto, customerUserId);
        return Ok(booking);
    }

    /// <summary>
    /// Customer rejects a quotation with a reason.
    /// </summary>
    [HttpPost("{id:int}/reject")]
    [Authorize(Roles = "Customer")]
    [ProducesResponseType(typeof(QuotationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuotationDto>> Reject(
        int id,
        [FromBody] RejectQuotationDto dto,
        CancellationToken cancellationToken)
    {
        var customerUserId = GetCurrentUserId();
        var updated = await _quotationService.RejectAsync(id, dto, customerUserId);
        return Ok(updated);
    }

    private string GetCurrentUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub")
                     ?? throw new UnauthorizedAccessException("User identity not found in token.");
        return userId;
    }
}