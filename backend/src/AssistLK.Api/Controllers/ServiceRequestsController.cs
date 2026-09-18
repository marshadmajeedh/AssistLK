using System.Security.Claims;
using AssistLK.Api.DTOs.ServiceRequests;
using AssistLK.Application.Interfaces;
using AssistLK.Application.ServiceRequests.DTOs;
using AssistLK.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssistLK.Api.Controllers;

[ApiController]
[Route("api/service-requests")]
[Authorize(Roles = "Customer")]
public class ServiceRequestsController : ControllerBase
{
    private readonly IServiceRequestService _serviceRequestService;
    private readonly ProblemUnderstandingWorkflowService _workflowService;

    public ServiceRequestsController(
        IServiceRequestService serviceRequestService,
        ProblemUnderstandingWorkflowService workflowService)
    {
        _serviceRequestService = serviceRequestService;
        _workflowService = workflowService;
    }

    [HttpPost]
    public async Task<ActionResult<ServiceRequestResponse>> Create(
        [FromBody] CreateServiceRequestRequest request,
        CancellationToken cancellationToken)
    {
        var customerId = GetCurrentCustomerId();
        var response = await _serviceRequestService.CreateAsync(
            customerId,
            request,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = response.ServiceRequestId },
            response);
    }

    [HttpGet("my")]
    public async Task<ActionResult<IReadOnlyList<ServiceRequestResponse>>> GetMyRequests(
        CancellationToken cancellationToken)
    {
        var customerId = GetCurrentCustomerId();
        var response = await _serviceRequestService.GetCurrentCustomerRequestsAsync(
            customerId,
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ServiceRequestResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var customerId = GetCurrentCustomerId();
        var response = await _serviceRequestService.GetByIdAsync(
            id,
            customerId,
            cancellationToken);

        return Ok(response);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ServiceRequestResponse>> Update(
        Guid id,
        [FromBody] UpdateServiceRequestRequest request,
        CancellationToken cancellationToken)
    {
        var customerId = GetCurrentCustomerId();
        var response = await _serviceRequestService.UpdateAsync(
            customerId,
            id,
            request,
            cancellationToken);

        return Ok(response);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<ServiceRequestResponse>> Cancel(
        Guid id,
        CancellationToken cancellationToken)
    {
        var customerId = GetCurrentCustomerId();
        var response = await _serviceRequestService.CancelAsync(
            customerId,
            id,
            cancellationToken);

        return Ok(response);
    }

    [HttpPost("{id:guid}/analyze")]
    public async Task<ActionResult<ProblemUnderstandingResponseDto>> Analyze(
        Guid id,
        CancellationToken cancellationToken)
    {
        var customerId = GetCurrentCustomerId();
        var result = await _workflowService.AnalyzeAsync(
            id,
            customerId,
            cancellationToken);

        if (!result.Success)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { message = result.ErrorMessage ?? "Problem understanding analysis failed." });
        }

        var response = new ProblemUnderstandingResponseDto
        {
            WorkflowId = result.WorkflowId,
            ExecutionId = result.ExecutionId,
            ServiceRequestId = result.ServiceRequestId,
            Status = result.Status,
            Category = result.Category,
            ProblemSummary = result.ProblemSummary,
            Urgency = result.Urgency,
            Confidence = result.Confidence,
            NeedsMoreInformation = result.NeedsMoreInformation,
            FollowUpQuestions = result.FollowUpQuestions
        };

        return Ok(response);
    }

    [HttpPost("{id:guid}/ready-for-matching")]
    public async Task<ActionResult<ServiceRequestResponse>> MarkReadyForMatching(
        Guid id,
        CancellationToken cancellationToken)
    {
        var customerId = GetCurrentCustomerId();
        var response = await _serviceRequestService.MarkReadyForMatchingAsync(
            id,
            customerId,
            cancellationToken);

        return Ok(response);
    }

    [HttpPost("{id:guid}/clarifications/answers")]
    public async Task<ActionResult<IReadOnlyList<ServiceRequestClarificationDto>>> SubmitClarificationAnswers(
        Guid id,
        [FromBody] SubmitClarificationAnswersRequest request,
        CancellationToken cancellationToken)
    {
        var customerId = GetCurrentCustomerId();
        var response = await _serviceRequestService.SubmitClarificationAnswersAsync(
            customerId,
            id,
            request,
            cancellationToken);

        return Ok(response);
    }

    private Guid GetCurrentCustomerId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(idClaim) || !Guid.TryParse(idClaim, out var customerId))
        {
            throw new UnauthorizedAccessException("Customer identifier is missing or invalid in the bearer token.");
        }

        return customerId;
    }
}
