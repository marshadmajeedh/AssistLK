namespace AssistLK.Agents.Configuration;

/// <summary>
/// Configuration options for agent services integration with the external Python LangGraph agent service.
/// </summary>
public class AgentServicesOptions
{
    public const string SectionName = "AgentServices";

    /// <summary>
    /// Base URL of the Python Problem Understanding Agent service (e.g. "http://127.0.0.1:8001").
    /// </summary>
    public string ProblemUnderstandingUrl { get; set; } = "http://127.0.0.1:8001";

    public string TrackingValidationUrl { get; set; } = "http://127.0.0.1:8000";

    /// <summary>
    /// Optional shared secret header for internal service-to-service authentication (X-Internal-Api-Key).
    /// </summary>
    public string? InternalApiKey { get; set; }

    /// <summary>
    /// HTTP request timeout in seconds for agent execution requests. Defaults to 45 seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 45;

    /// <summary>
    /// Validates that configuration options are valid.
    /// Throws InvalidOperationException if ProblemUnderstandingUrl is not an absolute HTTP/HTTPS URI or TimeoutSeconds <= 0.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProblemUnderstandingUrl) ||
            !Uri.TryCreate(ProblemUnderstandingUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"Invalid AgentServices:ProblemUnderstandingUrl '{ProblemUnderstandingUrl}'. It must be a valid absolute HTTP or HTTPS URL.");
        }

        if (string.IsNullOrWhiteSpace(TrackingValidationUrl) ||
            !Uri.TryCreate(TrackingValidationUrl, UriKind.Absolute, out var trackingUri) ||
            (trackingUri.Scheme != Uri.UriSchemeHttp && trackingUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"Invalid AgentServices:TrackingValidationUrl '{TrackingValidationUrl}'. It must be a valid absolute HTTP or HTTPS URL.");
        }

        if (TimeoutSeconds <= 0)
        {
            throw new InvalidOperationException(
                $"Invalid AgentServices:TimeoutSeconds '{TimeoutSeconds}'. Timeout must be greater than zero.");
        }
    }
}
