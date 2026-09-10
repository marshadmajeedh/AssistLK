using AssistLK.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.Api.Controllers;

[ApiController]
[Route("api/agent-monitoring")]
public class AgentMonitoringController :
    ControllerBase
{

    private readonly AssistLKDbContext _db;

    public AgentMonitoringController(
        AssistLKDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetMetrics()
    {

        var metrics =
            await _db.AgentExecutionMetrics
            .OrderByDescending(
                x => x.CreatedAtUtc)
            .ToListAsync();

        return Ok(metrics);
    }
}