using AssistLK.Application.Interfaces;
using AssistLK.Application.ServiceRequests.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssistLK.Api.Controllers;

[ApiController]
[Route("api/admin/service-requests")]
[Authorize(Roles = "Admin")]
public class AdminServiceRequestsController : ControllerBase
{
    private readonly IServiceRequestService _serviceRequestService;

    public AdminServiceRequestsController(IServiceRequestService serviceRequestService)
    {
        _serviceRequestService = serviceRequestService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ServiceRequestResponse>>> GetAll(
        [FromQuery] string? status,
        [FromQuery] string? category,
        [FromQuery] string? urgency,
        CancellationToken cancellationToken)
    {
        var response = await _serviceRequestService.GetAllForAdminAsync(
            status,
            category,
            urgency,
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ServiceRequestResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await _serviceRequestService.GetByIdForAdminAsync(
            id,
            cancellationToken);

        return Ok(response);
    }
}
