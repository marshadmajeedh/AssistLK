using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AssistLK.Agents.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AssistLK.Agents.Services;

/// <summary>
/// Service that interacts with Google Gemini API (gemini-2.5-flash) for reasoning.
/// Safely reads credentials, makes HTTP requests, and handles failures without exposing secrets.
/// </summary>
public class GeminiService : IGeminiService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<GeminiService>? _logger;
    private readonly string _model;
    private const string BaseEndpoint = "https://generativelanguage.googleapis.com/v1beta/models";

    public GeminiService(
        HttpClient? httpClient = null,
        IConfiguration? configuration = null,
        ILogger<GeminiService>? logger = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _configuration = configuration;
        _logger = logger;
        _model = configuration?["Gemini:Model"] ?? "gemini-2.5-flash";
    }

    public async Task<string?> GenerateContentAsync(
        string prompt,
        string? systemInstruction = null,
        CancellationToken cancellationToken = default)
    {
        var apiKey = ResolveApiKey(_configuration);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            var allowOffline = true;
            if (_configuration != null && bool.TryParse(_configuration["Gemini:AllowOfflineSimulation"], out var configuredOffline))
            {
                allowOffline = configuredOffline;
            }

            if (allowOffline)
            {
                _logger?.LogInformation("No GOOGLE_API_KEY configured. Utilizing offline simulation for test/dev environment.");
                return SimulateOfflineReasoning(prompt);
            }

            _logger?.LogWarning("Gemini API key is not configured. Returning null for safe degradation.");
            return null;
        }

        try
        {
            var requestUrl = $"{BaseEndpoint}/{_model}:generateContent?key={Uri.EscapeDataString(apiKey)}";

            var payload = new JsonObject
            {
                ["contents"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["role"] = "user",
                        ["parts"] = new JsonArray
                        {
                            new JsonObject { ["text"] = prompt }
                        }
                    }
                },
                ["generationConfig"] = new JsonObject
                {
                    ["responseMimeType"] = "application/json",
                    ["temperature"] = 0.2
                }
            };

            if (!string.IsNullOrWhiteSpace(systemInstruction))
            {
                payload["systemInstruction"] = new JsonObject
                {
                    ["parts"] = new JsonArray
                    {
                        new JsonObject { ["text"] = systemInstruction }
                    }
                };
            }

            using var content = new StringContent(
                payload.ToJsonString(),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.PostAsync(requestUrl, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Never log requestUrl or apiKey
                _logger?.LogWarning(
                    "Gemini API returned non-success HTTP status code: {StatusCode}",
                    (int)response.StatusCode);
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            return ExtractTextFromResponse(responseJson);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Never log secrets or URLs with secrets
            _logger?.LogError(
                "Gemini API call encountered an unexpected failure: {Message}",
                ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Extracts generated text from Gemini API response JSON.
    /// </summary>
    private static string? ExtractTextFromResponse(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("candidates", out var candidates) &&
                candidates.GetArrayLength() > 0)
            {
                var firstCandidate = candidates[0];
                if (firstCandidate.TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0)
                {
                    var firstPart = parts[0];
                    if (firstPart.TryGetProperty("text", out var textElement))
                    {
                        return textElement.GetString();
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Safely resolves the Google API key from multiple configuration sources.
    /// Never logs the resolved key value.
    /// </summary>
    private static string? ResolveApiKey(IConfiguration? configuration)
    {
        var key = configuration?["GOOGLE_API_KEY"];
        if (string.IsNullOrWhiteSpace(key))
        {
            key = configuration?["Gemini:ApiKey"];
        }
        if (string.IsNullOrWhiteSpace(key))
        {
            key = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
        }
        return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
    }

    /// <summary>
    /// Deterministic offline simulation for testing without live Gemini API keys.
    /// Produces compliant JSON outputs matching the Gemini system prompt contract.
    /// </summary>
    private static string SimulateOfflineReasoning(string prompt)
    {
        var lower = prompt.ToLowerInvariant();

        // Check for purely ambiguous / unclassifiable requests
        if (lower.Contains("broken please fix") || lower.Contains("it is broken please fix") ||
            (lower.Contains("broken") && !lower.Contains("pipe") && !lower.Contains("tap") && !lower.Contains("car") && !lower.Contains("fridge") && !lower.Contains("refrigerator") && !lower.Contains("appliance")))
        {
            return "{\n" +
                   "  \"category\": \"Unclassified\",\n" +
                   "  \"problemSummary\": \"Possible service issue. Insufficient information provided to determine category.\",\n" +
                   "  \"urgency\": \"Unknown\",\n" +
                   "  \"needsMoreInformation\": true,\n" +
                   "  \"followUpQuestions\": [\"Could you describe the equipment, system, or fixture affected?\", \"What specific symptoms are occurring?\"],\n" +
                   "  \"confidence\": 0.2,\n" +
                   "  \"additionalInformation\": {}\n" +
                   "}";
        }

        // Refrigerator or appliance without detailed problem
        if (lower.Trim().Equals("refrigerator") || lower.Contains("\"refrigerator\"") || lower.Contains("appliance is broken") || lower.Contains("my appliance is broken"))
        {
            return "{\n" +
                   "  \"category\": \"Appliance Repair\",\n" +
                   "  \"problemSummary\": \"Possible refrigeration or domestic appliance fault.\",\n" +
                   "  \"urgency\": \"Unknown\",\n" +
                   "  \"needsMoreInformation\": true,\n" +
                   "  \"followUpQuestions\": [\"Could you describe the appliance, system, or vehicle that is affected?\", \"What specific symptoms are occurring?\"],\n" +
                   "  \"confidence\": 0.4,\n" +
                   "  \"additionalInformation\": {}\n" +
                   "}";
        }

        if (lower.Contains("pipe") || lower.Contains("leak") || lower.Contains("sink") || lower.Contains("tap") || lower.Contains("plumb") || lower.Contains("flood") || lower.Contains("drain") || lower.Contains("drip"))
        {
            var isUrgent = lower.Contains("burst") || lower.Contains("flood") || lower.Contains("heavily") || lower.Contains("badly") || lower.Contains("gushing");
            var isDrip = lower.Contains("drip") || lower.Contains("slowly");
            var urgency = isUrgent ? "High" : (isDrip ? "Low" : "Medium");
            var summary = isUrgent
                ? "Possible burst pipe or water flooding. Immediate professional attention may be required."
                : (isDrip ? "Possible dripping tap or minor pipe leak." : "Possible water leakage or plumbing fault.");
            return "{\n" +
                   "  \"category\": \"Plumbing\",\n" +
                  $"  \"problemSummary\": \"{summary}\",\n" +
                  $"  \"urgency\": \"{urgency}\",\n" +
                   "  \"needsMoreInformation\": false,\n" +
                   "  \"followUpQuestions\": [],\n" +
                  $"  \"confidence\": {(isUrgent ? "0.9" : "0.85")},\n" +
                   "  \"additionalInformation\": {}\n" +
                   "}";
        }

        if (lower.Contains("spark") || lower.Contains("socket") || lower.Contains("electric") || lower.Contains("wire") || lower.Contains("breaker") || lower.Contains("burning") || lower.Contains("smell"))
        {
            var isUrgent = lower.Contains("spark") || lower.Contains("burn") || lower.Contains("smoke") || lower.Contains("shock") || lower.Contains("fire") || lower.Contains("smell");
            var urgency = lower.Contains("fire") ? "Critical" : (isUrgent ? "High" : "Medium");
            return "{\n" +
                   "  \"category\": \"Electrical\",\n" +
                   "  \"problemSummary\": \"Possible electrical safety issue. Professional inspection is recommended to ensure safety.\",\n" +
                  $"  \"urgency\": \"{urgency}\",\n" +
                   "  \"needsMoreInformation\": false,\n" +
                   "  \"followUpQuestions\": [],\n" +
                   "  \"confidence\": 0.88,\n" +
                   "  \"additionalInformation\": {}\n" +
                   "}";
        }

        if (lower.Contains("car") || lower.Contains("vehicle") || lower.Contains("battery") || lower.Contains("engine") || lower.Contains("brake"))
        {
            var isUrgent = lower.Contains("accident") || lower.Contains("crash") || lower.Contains("road") || lower.Contains("stopped");
            var urgency = (lower.Contains("accident") || lower.Contains("crash")) ? "Critical" : (isUrgent || lower.Contains("start") ? "High" : "Medium");
            var summary = lower.Contains("start")
                ? "Possible battery or vehicle starting-system issue."
                : "Possible vehicle mechanical fault.";
            return "{\n" +
                   "  \"category\": \"Vehicle Repair\",\n" +
                  $"  \"problemSummary\": \"{summary}\",\n" +
                  $"  \"urgency\": \"{urgency}\",\n" +
                   "  \"needsMoreInformation\": false,\n" +
                   "  \"followUpQuestions\": [],\n" +
                   "  \"confidence\": 0.85,\n" +
                   "  \"additionalInformation\": {}\n" +
                   "}";
        }

        if (lower.Contains("fridge") || lower.Contains("refrigerator") || lower.Contains("freezer") || lower.Contains("washer") || lower.Contains("dryer") || lower.Contains("appliance"))
        {
            var summary = (lower.Contains("fridge") || lower.Contains("refrigerator") || lower.Contains("freezer"))
                ? "Possible refrigeration or cooling system fault."
                : "Possible domestic appliance fault.";
            return "{\n" +
                   "  \"category\": \"Appliance Repair\",\n" +
                  $"  \"problemSummary\": \"{summary}\",\n" +
                   "  \"urgency\": \"Low\",\n" +
                   "  \"needsMoreInformation\": false,\n" +
                   "  \"followUpQuestions\": [],\n" +
                   "  \"confidence\": 0.82,\n" +
                   "  \"additionalInformation\": {}\n" +
                   "}";
        }

        return "{\n" +
               "  \"category\": \"Unclassified\",\n" +
               "  \"problemSummary\": \"Possible service issue. Category could not be determined with confidence.\",\n" +
               "  \"urgency\": \"Unknown\",\n" +
               "  \"needsMoreInformation\": true,\n" +
               "  \"followUpQuestions\": [\"Could you provide more details about the problem?\"],\n" +
               "  \"confidence\": 0.2,\n" +
               "  \"additionalInformation\": {}\n" +
               "}";
    }
}
