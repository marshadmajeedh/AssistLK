namespace AssistLK.Agents.Configuration;

/// <summary>
/// Configuration options for agent services integration.
/// Supports switching between native C# Gemini reasoning and the external Python LangGraph agent service.
/// </summary>
public class AgentServicesOptions
{
    public const string SectionName = "AgentServices";

    public const string NativeCSharpMode = "NativeCSharp";
    public const string ExternalPythonMode = "ExternalPython";

    public static readonly string[] ValidModes = new[]
    {
        NativeCSharpMode,
        ExternalPythonMode
    };

    /// <summary>
    /// Operating mode for Problem Understanding Agent: "NativeCSharp" or "ExternalPython".
    /// Defaults to "NativeCSharp" for backward compatibility.
    /// </summary>
    public string ProblemUnderstandingMode { get; set; } = NativeCSharpMode;

    /// <summary>
    /// Base URL of the Python Problem Understanding Agent service (e.g. "http://127.0.0.1:8001").
    /// </summary>
    public string ProblemUnderstandingUrl { get; set; } = "http://127.0.0.1:8001";

    /// <summary>
    /// Optional shared secret header for internal service-to-service authentication (X-Internal-Api-Key).
    /// </summary>
    public string? InternalApiKey { get; set; }

    /// <summary>
    /// HTTP request timeout in seconds for agent execution requests. Defaults to 45 seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 45;

    /// <summary>
    /// Validates that the configured mode is one of the supported modes.
    /// Throws InvalidOperationException if an unknown or invalid mode is specified.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProblemUnderstandingMode) ||
            !ValidModes.Contains(ProblemUnderstandingMode.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Invalid AgentServices:ProblemUnderstandingMode '{ProblemUnderstandingMode}'. " +
                $"Supported modes are '{NativeCSharpMode}' and '{ExternalPythonMode}'.");
        }
    }
}
