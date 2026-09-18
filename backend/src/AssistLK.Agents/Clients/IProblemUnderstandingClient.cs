using AssistLK.Agents.DTOs;

namespace AssistLK.Agents.Clients;

/// <summary>
/// Client interface for interacting with the internal Python Problem Understanding Agent service.
/// </summary>
public interface IProblemUnderstandingClient
{
    /// <summary>
    /// Executes the Python Problem Understanding agent via POST /agent/execute.
    /// </summary>
    /// <param name="request">Standardized execution request wire envelope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Execution response wire envelope containing authoritative result or sanitized failure.</returns>
    Task<AgentExecutionResponseDto> ExecuteAsync(
        AgentExecutionRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the health of the Python Problem Understanding agent service via GET /health.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Health check response or null if service is unreachable.</returns>
    Task<HealthCheckResponseDto?> GetHealthAsync(
        CancellationToken cancellationToken = default);
}
