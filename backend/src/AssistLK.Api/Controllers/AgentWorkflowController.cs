using AssistLK.Api.DTOs;
using AssistLK.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace AssistLK.Api.Controllers;

[ApiController]
[Route("api/agents")]
public class AgentWorkflowController :
    ControllerBase
{
    private readonly AgentExecutionService _service;

    public AgentWorkflowController(
        AgentExecutionService service)
    {
        _service = service;
    }

    [HttpPost("execute")]
    public async Task<IActionResult> Execute(
        AgentWorkflowRequestDto request)
    {
        var result =
            await _service.ExecuteAsync(
                null,
                request.AgentName,
                request.Input
            );

        return Ok(result);
    }
}