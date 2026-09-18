using AssistLK.Agents.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace AssistLK.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    private readonly IAssistantAgent _agent;

    public AgentController(IAssistantAgent agent)
    {
        _agent = agent;
    }

    [HttpPost("process")]
    public async Task<IActionResult> ProcessRequest([FromBody] string prompt)
    {
        var result = await _agent.ExecuteTaskAsync(prompt);
        return Ok(new { success = true, result });
    }
}