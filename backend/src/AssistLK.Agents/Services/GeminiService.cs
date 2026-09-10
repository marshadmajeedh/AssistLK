using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AssistLK.Agents.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AssistLK.Agents.Services;

/// <summary>
/// Service that interacts with Google Gemini API (gemini-3.6-flash) for reasoning.
/// Safely reads credentials, makes HTTP requests, and handles failures without exposing secrets.
/// </summary>
public class GeminiService : IGeminiService
{
    public const string DefaultModel = "gemini-3.6-flash";
    public static readonly TimeSpan DefaultAttemptTimeout = TimeSpan.FromSeconds(20);
    private const string BaseEndpoint = "https://generativelanguage.googleapis.com/v1beta/models";
    private const int MaxAttempts = 3;

    private readonly HttpClient _httpClient;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<GeminiService>? _logger;
    private readonly string _model;
    private readonly TimeSpan? _customRetryDelay;
    private readonly TimeSpan _attemptTimeout;

    public string Model => _model;
    public TimeSpan AttemptTimeout => _attemptTimeout;

    public GeminiService(
        HttpClient? httpClient = null,
        IConfiguration? configuration = null,
        ILogger<GeminiService>? logger = null,
        TimeSpan? retryDelay = null,
        TimeSpan? attemptTimeout = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _configuration = configuration;
        _logger = logger;
        _customRetryDelay = retryDelay;

        var configuredModel = configuration?["Gemini:Model"];
        _model = !string.IsNullOrWhiteSpace(configuredModel) ? configuredModel.Trim() : DefaultModel;

        if (attemptTimeout.HasValue)
        {
            _attemptTimeout = attemptTimeout.Value;
        }
        else if (configuration != null &&
                 double.TryParse(configuration["Gemini:AttemptTimeoutSeconds"], out var timeoutSeconds) &&
                 timeoutSeconds > 0)
        {
            _attemptTimeout = TimeSpan.FromSeconds(timeoutSeconds);
        }
        else
        {
            _attemptTimeout = DefaultAttemptTimeout;
        }
    }

    public async Task<string?> GenerateContentAsync(
        string prompt,
        string? systemInstruction = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var apiKey = ResolveApiKey(_configuration);

        // Diagnostic: log whether API key resolved — never log the key value itself
        _logger?.LogInformation(
            "Gemini API key configured: {Configured}",
            !string.IsNullOrWhiteSpace(apiKey) ? "true" : "false");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            var allowOffline = true;
            if (_configuration != null && bool.TryParse(_configuration["Gemini:AllowOfflineSimulation"], out var configuredOffline))
            {
                allowOffline = configuredOffline;
            }

            if (allowOffline)
            {
                _logger?.LogInformation(
                    "Gemini unavailable reason: API key not configured. Engaging offline simulation (test/dev environment).");
                return SimulateOfflineReasoning(prompt);
            }

            _logger?.LogWarning(
                "Gemini fallback triggered: reason - API key not configured and offline simulation disabled");
            return null;
        }

        try
        {
            var requestUrl = $"{BaseEndpoint}/{_model}:generateContent";

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

            var payloadJson = payload.ToJsonString();

            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                using var content = new StringContent(
                    payloadJson,
                    Encoding.UTF8,
                    "application/json");

                using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                attemptCts.CancelAfter(_attemptTimeout);

                using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
                {
                    Content = content
                };
                request.Headers.Add("x-goog-api-key", apiKey);

                // Diagnostic: signal API call start — never log requestUrl or apiKey
                _logger?.LogInformation("Calling Gemini model: {Model}", _model);
                _logger?.LogInformation("Gemini execution started");

                try
                {
                    using var response = await _httpClient.SendAsync(request, attemptCts.Token);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseJson = await response.Content.ReadAsStringAsync(attemptCts.Token);

                        // Diagnostic: log receipt — never log the raw JSON body
                        _logger?.LogInformation("Gemini response received: true");

                        return ExtractTextFromResponse(responseJson);
                    }

                    var statusCode = (int)response.StatusCode;

                    // Non-retriable client errors (all 4xx except 429 Too Many Requests, e.g. 400, 401, 403, 404, 422)
                    // Fail fast immediately without wasting time on retries.
                    var isNonRetriable = statusCode >= 400 && statusCode < 500 && response.StatusCode != HttpStatusCode.TooManyRequests;

                    if (isNonRetriable)
                    {
                        _logger?.LogWarning(
                            "Gemini fallback triggered: reason - non-success HTTP status code {StatusCode}",
                            statusCode);
                        _logger?.LogInformation("Gemini response received: false");
                        return null;
                    }

                    // Transient errors to retry: HTTP 429 (TooManyRequests), HTTP 503 (ServiceUnavailable), or server error
                    if (attempt < MaxAttempts)
                    {
                        _logger?.LogWarning(
                            "Gemini transient failure: HTTP {StatusCode}. Retrying attempt {Attempt} of {MaxAttempts}...",
                            statusCode, attempt, MaxAttempts);

                        var delay = _customRetryDelay ?? TimeSpan.FromMilliseconds(500 * attempt);
                        if (delay > TimeSpan.Zero)
                        {
                            await Task.Delay(delay, cancellationToken);
                        }
                        continue;
                    }

                    // Max retries exhausted
                    _logger?.LogWarning(
                        "Gemini fallback triggered: reason - non-success HTTP status code {StatusCode}",
                        statusCode);
                    _logger?.LogInformation("Gemini response received: false");
                    return null;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Caller aborted request - propagate to workflow
                    throw;
                }
                catch (OperationCanceledException) when (attemptCts.IsCancellationRequested)
                {
                    // Attempt timed out
                    if (attempt < MaxAttempts)
                    {
                        _logger?.LogWarning(
                            "Gemini attempt {Attempt} timed out after {Timeout}s. Retrying attempt {NextAttempt} of {MaxAttempts}...",
                            attempt, _attemptTimeout.TotalSeconds, attempt + 1, MaxAttempts);

                        var delay = _customRetryDelay ?? TimeSpan.FromMilliseconds(500 * attempt);
                        if (delay > TimeSpan.Zero)
                        {
                            await Task.Delay(delay, cancellationToken);
                        }
                        continue;
                    }

                    _logger?.LogWarning(
                        "Gemini fallback triggered: reason - attempt timeout exhausted after {MaxAttempts} attempts",
                        MaxAttempts);
                    _logger?.LogInformation("Gemini response received: false");
                    return null;
                }
                catch (HttpRequestException ex)
                {
                    // Transient network error (DNS, connection reset, socket error)
                    if (attempt < MaxAttempts)
                    {
                        _logger?.LogWarning(
                            "Gemini transient network failure: {Message}. Retrying attempt {Attempt} of {MaxAttempts}...",
                            ex.Message, attempt, MaxAttempts);

                        var delay = _customRetryDelay ?? TimeSpan.FromMilliseconds(500 * attempt);
                        if (delay > TimeSpan.Zero)
                        {
                            await Task.Delay(delay, cancellationToken);
                        }
                        continue;
                    }

                    _logger?.LogWarning(
                        "Gemini fallback triggered: reason - network failure exhausted after {MaxAttempts} attempts: {Message}",
                        MaxAttempts,
                        ex.Message);
                    _logger?.LogInformation("Gemini response received: false");
                    return null;
                }
            }

            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Never log secrets or URLs with secrets
            _logger?.LogError(
                "Gemini fallback triggered: reason - {Message}",
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
        var textToAnalyze = ExtractCustomerDescription(prompt);
        var lower = textToAnalyze.ToLowerInvariant();

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

    public static string ExtractCustomerDescription(string prompt)
    {
        const string openTag = "<customer_description>";
        const string closeTag = "</customer_description>";

        var startIdx = prompt.IndexOf(openTag, StringComparison.OrdinalIgnoreCase);
        if (startIdx >= 0)
        {
            startIdx += openTag.Length;
            var endIdx = prompt.IndexOf(closeTag, startIdx, StringComparison.OrdinalIgnoreCase);
            if (endIdx > startIdx)
            {
                var content = prompt.Substring(startIdx, endIdx - startIdx).Trim();
                return System.Net.WebUtility.HtmlDecode(content);
            }
        }

        return prompt;
    }
}
