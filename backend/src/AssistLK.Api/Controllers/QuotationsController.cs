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
        var created = await _quotationService.CreateAsync(dto, providerUserId, cancellationToken);
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
    [HttpGet("by-request/{serviceRequestId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<QuotationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<QuotationDto>>> GetByServiceRequest(
        Guid serviceRequestId,
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
    [ProducesResponseType(typeof(QuotationApprovalWorkflowDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuotationApprovalWorkflowDto>> SendForApproval(
        int id,
        CancellationToken cancellationToken)
    {
        var providerUserId = GetCurrentUserId();
        var workflow = await _quotationService.StartApprovalWorkflowAsync(
            id, providerUserId, cancellationToken);
        return Ok(workflow);
    }

    /// <summary>
    /// Business-specific operation: Customer approves a quotation.
    /// Atomically transitions the quotation to Approved AND creates a Booking.
    ///
    /// The agent workflow resumes first; ASP.NET then applies the decision
    /// using the authenticated customer's identity.
    /// </summary>
    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = "Customer,Provider")]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDto>> Approve(
        int id,
        [FromBody] ApproveQuotationDto dto,
        CancellationToken cancellationToken)
    {
        if (dto is null)
        {
            return BadRequest("Request body is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.ThreadId))
        {
            return BadRequest("threadId is required; start the quotation workflow first.");
        }

        await _quotationService.ResumeApprovalWorkflowAsync(
            id, dto.ThreadId, "approve", dto.CustomerRemarks, cancellationToken);

        var customerUserId = GetCurrentUserId();
        var booking = await _quotationService.ApproveAsync(
            id, dto, customerUserId, cancellationToken);
        return Ok(booking);
    }

    /// <summary>
    /// Customer rejects a quotation with a reason.
    ///
    /// The agent workflow resumes first; ASP.NET then applies the decision
    /// using the authenticated customer's identity.
    /// </summary>
    [HttpPost("{id:int}/reject")]
    [Authorize(Roles = "Customer,Provider")]
    [ProducesResponseType(typeof(QuotationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuotationDto>> Reject(
        int id,
        [FromBody] RejectQuotationDto dto,
        CancellationToken cancellationToken)
    {
        if (dto is null)
        {
            return BadRequest("Request body is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.ThreadId))
        {
            return BadRequest("threadId is required; start the quotation workflow first.");
        }

        await _quotationService.ResumeApprovalWorkflowAsync(
            id, dto.ThreadId, "reject", dto.Reason, cancellationToken);

        var customerUserId = GetCurrentUserId();
        var updated = await _quotationService.RejectAsync(
            id, dto, customerUserId, cancellationToken);
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