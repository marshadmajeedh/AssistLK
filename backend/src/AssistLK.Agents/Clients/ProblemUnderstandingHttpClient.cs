using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AssistLK.Agents.Configuration;
using AssistLK.Agents.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssistLK.Agents.Clients;

/// <summary>
/// Typed HTTP client for dispatching requests to the internal Python FastAPI / LangGraph
/// Problem Understanding Agent service.
/// </summary>
public class ProblemUnderstandingHttpClient : IProblemUnderstandingClient
{
    private const string ExecuteEndpoint = "/agent/execute";
    private const string HealthEndpoint = "/health";
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";

    private readonly HttpClient _httpClient;
    private readonly AgentServicesOptions _options;
    private readonly ILogger<ProblemUnderstandingHttpClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ProblemUnderstandingHttpClient(
        HttpClient httpClient,
        AgentServicesOptions? options = null,
        ILogger<ProblemUnderstandingHttpClient>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? new AgentServicesOptions();
        _logger = logger ?? NullLogger<ProblemUnderstandingHttpClient>.Instance;

        // Apply timeout if not already configured on the HttpClient
        if (_httpClient.Timeout == Timeout.InfiniteTimeSpan || _httpClient.Timeout > TimeSpan.FromSeconds(_options.TimeoutSeconds))
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds > 0 ? _options.TimeoutSeconds : 45);
        }

        // Apply BaseAddress if not already configured
        if (_httpClient.BaseAddress == null && !string.IsNullOrWhiteSpace(_options.ProblemUnderstandingUrl))
        {
            var baseUrl = _options.ProblemUnderstandingUrl.TrimEnd('/');
            _httpClient.BaseAddress = new Uri(baseUrl);
        }
    }

    /// <inheritdoc />
    public async Task<AgentExecutionResponseDto> ExecuteAsync(
        AgentExecutionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestJson = JsonSerializer.Serialize(request, JsonOptions);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ExecuteEndpoint)
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };

        // Security boundary: Only send X-Internal-Api-Key when a non-empty value is configured.
        // Never forward customer JWT, refresh tokens, passwords, or LLM provider API keys.
        ApplySecurityHeaders(httpRequest);

        _logger.LogInformation(
            "Dispatching execution request {RequestId} to internal agent service at {Endpoint}",
            request.RequestId,
            ExecuteEndpoint);

        try
        {
            using var response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var statusCode = (int)response.StatusCode;
                var rawBody = await response.Content.ReadAsStringAsync(cancellationToken);

                _logger.LogWarning(
                    "Python agent service returned non-success HTTP status {StatusCode} for request {RequestId}",
                    statusCode,
                    request.RequestId);

                string sanitizedDetail;
                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    sanitizedDetail = $"Internal service authentication failed (HTTP {statusCode}).";
                }
                else if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
                {
                    sanitizedDetail = $"Payload validation failed at agent service (HTTP 422): {Truncate(rawBody, 200)}";
                }
                else
                {
                    sanitizedDetail = $"Agent service error (HTTP {statusCode}).";
                }

                return new AgentExecutionResponseDto
                {
                    RequestId = request.RequestId,
                    Success = false,
                    ErrorMessage = sanitizedDetail,
                    Metadata = new ExecutionMetadataPayloadDto
                    {
                        AgentName = request.AgentName,
                        Provider = "unknown",
                        Degraded = true,
                        DurationMs = 0
                    }
                };
            }

            var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var deserialized = await JsonSerializer.DeserializeAsync<AgentExecutionResponseDto>(
                contentStream,
                JsonOptions,
                cancellationToken);

            if (deserialized == null)
            {
                _logger.LogWarning(
                    "Python agent service returned empty or null JSON payload for request {RequestId}",
                    request.RequestId);

                return new AgentExecutionResponseDto
                {
                    RequestId = request.RequestId,
                    Success = false,
                    ErrorMessage = "Empty JSON response returned from agent service.",
                    Metadata = new ExecutionMetadataPayloadDto
                    {
                        AgentName = request.AgentName,
                        Provider = "unknown",
                        Degraded = true
                    }
                };
            }

            return deserialized;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Execution request {RequestId} timed out after {TimeoutSeconds}s while communicating with agent service",
                request.RequestId,
                _options.TimeoutSeconds);

            return new AgentExecutionResponseDto
            {
                RequestId = request.RequestId,
                Success = false,
                ErrorMessage = $"Agent service request timed out after {_options.TimeoutSeconds}s.",
                Metadata = new ExecutionMetadataPayloadDto
                {
                    AgentName = request.AgentName,
                    Provider = "unknown",
                    Degraded = true
                }
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(
                ex,
                "Transport failure connecting to Python agent service for request {RequestId}: {Message}",
                request.RequestId,
                ex.Message);

            return new AgentExecutionResponseDto
            {
                RequestId = request.RequestId,
                Success = false,
                ErrorMessage = $"Connection failure reaching agent service: {ex.Message}",
                Metadata = new ExecutionMetadataPayloadDto
                {
                    AgentName = request.AgentName,
                    Provider = "unknown",
                    Degraded = true
                }
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to deserialize agent service response for request {RequestId}: {Message}",
                request.RequestId,
                ex.Message);

            return new AgentExecutionResponseDto
            {
                RequestId = request.RequestId,
                Success = false,
                ErrorMessage = "Malformed JSON response received from agent service.",
                Metadata = new ExecutionMetadataPayloadDto
                {
                    AgentName = request.AgentName,
                    Provider = "unknown",
                    Degraded = true
                }
            };
        }
    }

    /// <inheritdoc />
    public async Task<HealthCheckResponseDto?> GetHealthAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, HealthEndpoint);
            ApplySecurityHeaders(request);

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonSerializer.DeserializeAsync<HealthCheckResponseDto>(
                contentStream,
                JsonOptions,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check failed for Python agent service: {Message}", ex.Message);
            return null;
        }
    }

    private void ApplySecurityHeaders(HttpRequestMessage request)
    {
        // Internal service auth key: only attach when configured and non-empty
        if (!string.IsNullOrWhiteSpace(_options.InternalApiKey))
        {
            request.Headers.Remove(InternalApiKeyHeader);
            request.Headers.Add(InternalApiKeyHeader, _options.InternalApiKey.Trim());
        }

        // Explicitly clear any Authorization headers to ensure customer JWT is NEVER forwarded
        request.Headers.Authorization = null;
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
    }
}
