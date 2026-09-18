using Microsoft.AspNetCore.Mvc;
using AssistLK.Agents.Core;
using AssistLK.Agents.DTOs;

namespace AssistLK.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ValidationController : ControllerBase
{
    private readonly ValidationSafetyAgent _safetyAgent;

    public ValidationController(ValidationSafetyAgent safetyAgent)
    {
        _safetyAgent = safetyAgent;
    }

    [HttpPost("validate-status")]
    public async Task<IActionResult> ValidateStatus([FromBody] ValidationRequestDto request)
    {
        var result = await _safetyAgent.ValidateTransitionAsync(request);
        return Ok(result);
    }

    // 🆕 Safety & Sentiment Analysis Endpoint
    [HttpPost("analyze-sentiment")]
    public async Task<IActionResult> AnalyzeSentiment([FromBody] SentimentRequestDto request)
    {
        var result = await _safetyAgent.AnalyzeSentimentAsync(request);
        return Ok(result);
    }
}