using Microsoft.AspNetCore.Mvc;

namespace AssistLK.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SystemController : ControllerBase
{
    [HttpGet("info")]
    public IActionResult GetSystemInfo()
    {
        return Ok(new
        {
            application = "AssistLK",
            apiVersion = "v1",
            status = "Running"
        });
    }
}