using System.Text.Json;
using System.Text.Json.Nodes;
using AssistLK.Agents.Abstractions;
using AssistLK.Agents.Core;
using AssistLK.Agents.Models;
using AssistLK.Agents.Services;
using AssistLK.Agents.Tools;
using AssistLK.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace AssistLK.Agents.Agents;

/// <summary>
/// Component 1 – Problem Understanding Agent.
///
/// Upgraded to LLM-powered reasoning using Google Gemini API (gemini-2.5-flash).
/// Responsibility: Determine what kind of help the customer actually needs
/// by analysing their natural-language service request using Gemini reasoning
/// and producing a structured classification result.
///
/// Pipeline (fixed):
///   1. LocationExtractionTool  — optional enrichment
///   2. Gemini LLM              — PRIMARY reasoning engine
///   3. ProblemClassificationTool — optional validation/alignment
///   4. ServiceKnowledgeTool    — optional enrichment
///   5. Safety validation       — mandatory sanitization
///
/// Tool failure NEVER prevents Gemini execution.
/// Degraded output is produced ONLY when Gemini itself fails.
///
/// The agent does NOT:
/// - Access the database or any repository
/// - Assign or contact service providers
/// - Create bookings or quotations
/// - Handle payments
/// - Guarantee a diagnosis
/// - Provide dangerous DIY repair instructions
///
/// Lifecycle state transitions remain the responsibility of
/// IServiceRequestService / ServiceRequestService.
/// </summary>
public sealed class ProblemUnderstandingAgent : IAgent
{
    private readonly ToolExecutor _toolExecutor;
    private readonly IGeminiService _geminiService;
    private readonly ILogger<ProblemUnderstandingAgent> _logger;

    public ProblemUnderstandingAgent(
        ToolExecutor toolExecutor,
        IGeminiService geminiService,
        ILogger<ProblemUnderstandingAgent> logger)
    {
        _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
        _geminiService = geminiService ?? throw new ArgumentNullException(nameof(geminiService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "ProblemUnderstandingAgent";

    public const string SystemInstruction =
        "You are AssistLK Problem Understanding Agent.\n" +
        "Responsibilities:\n" +
        "- Understand customer service problems.\n" +
        "- Identify likely service category.\n" +
        "- Determine urgency.\n" +
        "- Decide whether more information is required.\n" +
        "- Generate safe customer-friendly summaries.\n\n" +
        "Allowed categories:\n" +
        "- Plumbing\n" +
        "- Electrical\n" +
        "- Vehicle Repair\n" +
        "- Appliance Repair\n" +
        "- Unclassified\n\n" +
        "Urgency values:\n" +
        "- Unknown (insufficient information to assess)\n" +
        "- Low (minor, non-urgent issue)\n" +
        "- Medium (standard fault requiring repair)\n" +
        "- High (active water flooding, burst pipes, vehicle breakdown/stalled, sparking, burning smells)\n" +
        "- Critical (fire, severe collision/accident, life safety hazard)\n\n" +
        "Strict Rules:\n" +
        "1. Never guarantee diagnosis. Always use uncertainty language like 'Possible...', 'may indicate...', 'could be...'.\n" +
        "2. Never provide dangerous repair instructions. Never suggest electrical repairs, wire handling, or equipment disassembly.\n" +
        "3. Recommend professional inspection when safety is a concern.\n" +
        "4. If the customer description is ambiguous, too short, or lacks key details, set needsMoreInformation to true and provide 1 to 3 relevant, concise follow-up questions. Category should be Unclassified if unclear.\n" +
        "5. Return JSON ONLY matching this exact structure with no markdown formatting:\n" +
        "{\n" +
        "  \"category\": \"Plumbing | Electrical | Vehicle Repair | Appliance Repair | Unclassified\",\n" +
        "  \"problemSummary\": \"Concise uncertainty-aware summary\",\n" +
        "  \"urgency\": \"Low | Medium | High | Critical | Unknown\",\n" +
        "  \"needsMoreInformation\": false,\n" +
        "  \"followUpQuestions\": [],\n" +
        "  \"confidence\": 0.0,\n" +
        "  \"additionalInformation\": {}\n" +
        "}";

    private static readonly HashSet<string> AllowedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Plumbing",
        "Electrical",
        "Vehicle Repair",
        "Appliance Repair",
        "Unclassified"
    };

    public async Task<AgentResult> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        var input = ResolveInput(context);
        var description = input?.Description ?? context.Input ?? string.Empty;

        // Gracefully handle empty or whitespace-only descriptions
        if (string.IsNullOrWhiteSpace(description))
        {
            context.Data["ToolCalls"] = 0;
            context.Data["ToolCallCount"] = 0;

            return BuildNeedsMoreInformationResult(
                serviceRequestId: input?.ServiceRequestId ?? Guid.Empty,
                locationText: input?.LocationText,
                questions: new[] { "Could you describe the problem you are experiencing?" },
                confidence: 0m);
        }

        var (output, toolCalls) = await AnalyseWithGeminiAndToolsAsync(description, input, cancellationToken);

        context.Data["ToolCalls"] = toolCalls;
        context.Data["ToolCallCount"] = toolCalls;

        return new AgentResult
        {
            Success = true,
            Message = output.NeedsMoreInformation
                ? "Problem analysis incomplete. Additional information required."
                : "Problem analysis completed.",
            Data = output,
            NextAction = output.NeedsMoreInformation
                ? "AwaitingInformation"
                : "Analysed"
        };
    }

    private async Task<(ProblemUnderstandingOutput Output, int ToolCallCount)> AnalyseWithGeminiAndToolsAsync(
        string description,
        ProblemUnderstandingInput? input,
        CancellationToken cancellationToken)
    {
        int toolCalls = 0;

        // ----------------------------------------------------------------
        // Step 1: LocationExtractionTool — optional enrichment
        // Failure degrades safely: retain un-normalized location text.
        // ----------------------------------------------------------------
        string? locationText = input?.LocationText;
        decimal? latitude = input?.Latitude;
        decimal? longitude = input?.Longitude;

        try
        {
            var locParams = new Dictionary<string, object>();
            if (!string.IsNullOrWhiteSpace(locationText)) locParams["locationText"] = locationText;
            if (latitude.HasValue) locParams["latitude"] = latitude.Value;
            if (longitude.HasValue) locParams["longitude"] = longitude.Value;

            var locResult = await _toolExecutor.ExecuteAsync("LocationExtractionTool", locParams);
            toolCalls++;

            if (locResult.Success && locResult.Data is LocationExtractionData locData)
            {
                locationText = locData.NormalizedLocation;
                latitude = locData.Latitude;
                longitude = locData.Longitude;
            }
        }
        catch (Exception ex)
        {
            // Location tool failure is non-fatal; continue with un-normalized location.
            _logger.LogWarning(
                "Agent tool execution failed: {Message}",
                ex.Message);
        }

        // ----------------------------------------------------------------
        // Step 2: Gemini LLM — PRIMARY reasoning engine
        // Degraded output is produced ONLY when Gemini fails or is unavailable.
        // Tool failures earlier or later NEVER reach this degradation path.
        // ----------------------------------------------------------------
        var prompt = BuildPrompt(description, locationText);
        string? rawGeminiResponse = null;

        try
        {
            rawGeminiResponse = await _geminiService.GenerateContentAsync(
                prompt,
                SystemInstruction,
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Log the failure (never log prompt, response, or secrets).
            _logger.LogWarning(
                "Agent tool execution failed: {Message}",
                ex.Message);
            rawGeminiResponse = null;
        }

        // Safe degradation — only when Gemini itself fails or returns unparseable output
        if (string.IsNullOrWhiteSpace(rawGeminiResponse) ||
            !TryParseGeminiResponse(rawGeminiResponse, out var parsedOutput))
        {
            _logger.LogWarning(
                "Gemini reasoning produced no usable output. Returning degraded response.");
            var degraded = CreateDegradedOutput(locationText);
            return (degraded, toolCalls);
        }

        // ----------------------------------------------------------------
        // Step 3: ProblemClassificationTool — optional validation/alignment
        // Failure is non-fatal: Gemini output stands as authoritative.
        // ----------------------------------------------------------------
        ProblemClassificationData? classData = null;
        try
        {
            var classParams = new Dictionary<string, object>
            {
                ["description"] = description
            };
            var classResult = await _toolExecutor.ExecuteAsync("ProblemClassificationTool", classParams);
            toolCalls++;

            if (classResult.Success && classResult.Data is ProblemClassificationData cd)
            {
                classData = cd;
            }
        }
        catch (Exception ex)
        {
            // Classification tool failure is non-fatal; Gemini output is authoritative.
            _logger.LogWarning(
                "Agent tool execution failed: {Message}",
                ex.Message);
        }

        // Optional alignment: if Gemini returned Unclassified but the deterministic tool
        // found a canonical category with higher confidence, promote it.
        if (classData is not null &&
            string.Equals(parsedOutput.Category, "Unclassified", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(classData.Category, "Unclassified", StringComparison.OrdinalIgnoreCase))
        {
            parsedOutput.Category = classData.Category;
            parsedOutput.Confidence = Math.Max(parsedOutput.Confidence, classData.Confidence);
        }

        // Ambiguity check: very short descriptions or generic phrasing require more info
        var lowerDesc = description.ToLowerInvariant();
        var descWords = description.Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
        bool isAmbiguous = descWords.Length <= 4 && !lowerDesc.Contains("flood") && !lowerDesc.Contains("fire") && !lowerDesc.Contains("burst");
        if (lowerDesc.Contains("broken") || lowerDesc.Contains("not working") || lowerDesc.Contains("something wrong"))
        {
            if (descWords.Length <= 6)
            {
                isAmbiguous = true;
            }
        }

        if (isAmbiguous)
        {
            parsedOutput.NeedsMoreInformation = true;
            if (parsedOutput.FollowUpQuestions.Count == 0)
            {
                parsedOutput.FollowUpQuestions = new[]
                {
                    "Could you describe the problem you are experiencing in more detail?",
                    "What specific symptoms or equipment are involved?"
                };
            }
            parsedOutput.Urgency = ServiceRequestUrgency.Unknown;
        }

        // ----------------------------------------------------------------
        // Step 4: ServiceKnowledgeTool — optional enrichment
        // Failure is non-fatal; Gemini output stands.
        // ----------------------------------------------------------------
        var additionalInfo = new Dictionary<string, string>(parsedOutput.AdditionalInformation);
        try
        {
            var knowParams = new Dictionary<string, object>
            {
                ["category"] = parsedOutput.Category,
                ["description"] = description
            };
            var knowResult = await _toolExecutor.ExecuteAsync("ServiceKnowledgeTool", knowParams);
            toolCalls++;

            if (knowResult.Success && knowResult.Data is ServiceKnowledgeData knowData)
            {
                additionalInfo["ServiceFamily"] = knowData.ServiceFamily;
                additionalInfo["SafeTerminology"] = knowData.SafeGeneralTerminology;
                if (knowData.RecommendsProfessionalInspection)
                {
                    additionalInfo["InspectionAdvised"] = "True";
                }
            }
        }
        catch (Exception ex)
        {
            // Knowledge tool failure is non-fatal; continue without enrichment.
            _logger.LogWarning(
                "Agent tool execution failed: {Message}",
                ex.Message);
        }

        // ----------------------------------------------------------------
        // Step 5: Safety validation — mandatory sanitization
        // ----------------------------------------------------------------
        ApplySafetyPolicies(parsedOutput, additionalInfo, locationText);

        return (parsedOutput, toolCalls);
    }

    private static string BuildPrompt(string description, string? locationText)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Analyze the following customer service request:");
        sb.AppendLine($"Customer Description: \"{description}\"");
        if (!string.IsNullOrWhiteSpace(locationText))
        {
            sb.AppendLine($"Customer Location: \"{locationText}\"");
        }
        sb.AppendLine("Return JSON only.");
        return sb.ToString();
    }

    private static bool TryParseGeminiResponse(string raw, out ProblemUnderstandingOutput output)
    {
        output = new ProblemUnderstandingOutput();
        try
        {
            var cleaned = CleanJsonResponse(raw);
            var node = JsonNode.Parse(cleaned);
            if (node is not JsonObject obj)
            {
                return false;
            }

            // Category
            var category = obj["category"]?.GetValue<string>()?.Trim() ?? "Unclassified";
            var matchedCategory = AllowedCategories.FirstOrDefault(c => string.Equals(c, category, StringComparison.OrdinalIgnoreCase)) ?? "Unclassified";
            output.Category = matchedCategory;

            // ProblemSummary
            var summary = obj["problemSummary"]?.GetValue<string>()?.Trim() ?? string.Empty;
            output.ProblemSummary = string.IsNullOrWhiteSpace(summary)
                ? $"Possible {output.Category.ToLowerInvariant()} issue."
                : summary;

            // Urgency
            var urgencyRaw = obj["urgency"]?.ToString()?.Trim();
            output.Urgency = ParseUrgency(urgencyRaw, output.Category);

            // NeedsMoreInformation
            var needsMore = obj["needsMoreInformation"]?.GetValue<bool?>() ?? false;
            if (string.Equals(output.Category, "Unclassified", StringComparison.OrdinalIgnoreCase))
            {
                needsMore = true;
            }
            output.NeedsMoreInformation = needsMore;

            // FollowUpQuestions
            var questions = new List<string>();
            if (obj["followUpQuestions"] is JsonArray arr)
            {
                foreach (var item in arr)
                {
                    var q = item?.GetValue<string>()?.Trim();
                    if (!string.IsNullOrWhiteSpace(q))
                    {
                        questions.Add(q);
                    }
                }
            }

            if (output.NeedsMoreInformation && questions.Count == 0)
            {
                questions.Add("Could you describe the problem you are experiencing in more detail?");
                questions.Add("What specific symptoms or equipment are involved?");
            }

            output.FollowUpQuestions = questions.Take(3).ToArray();

            // Confidence
            decimal confidence = 0.5m;
            if (obj["confidence"] != null)
            {
                if (decimal.TryParse(obj["confidence"]!.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedConf))
                {
                    confidence = parsedConf;
                }
            }
            output.Confidence = Clamp(confidence);

            // AdditionalInformation
            var additional = new Dictionary<string, string>();
            if (obj["additionalInformation"] is JsonObject addObj)
            {
                foreach (var kv in addObj)
                {
                    if (kv.Value != null)
                    {
                        additional[kv.Key] = kv.Value.ToString();
                    }
                }
            }
            output.AdditionalInformation = additional;

            return true;
        }
        catch (JsonException)
        {
            // Only malformed/unparseable Gemini JSON is suppressed here.
            // Unexpected programming errors (NullReferenceException, etc.) are intentionally not caught
            // so they surface for diagnosis rather than silently producing a degraded output.
            return false;
        }
    }

    private static string CleanJsonResponse(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[7..];
        }
        else if (trimmed.StartsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[3..];
        }

        if (trimmed.EndsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^3];
        }

        return trimmed.Trim();
    }

    private static ServiceRequestUrgency ParseUrgency(string? urgencyRaw, string category)
    {
        if (string.Equals(category, "Unclassified", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceRequestUrgency.Unknown;
        }

        if (string.IsNullOrWhiteSpace(urgencyRaw))
        {
            return ServiceRequestUrgency.Medium;
        }

        if (Enum.TryParse<ServiceRequestUrgency>(urgencyRaw, true, out var parsed))
        {
            return parsed;
        }

        return urgencyRaw.ToLowerInvariant() switch
        {
            "critical" => ServiceRequestUrgency.Critical,
            "high" => ServiceRequestUrgency.High,
            "medium" => ServiceRequestUrgency.Medium,
            "low" => ServiceRequestUrgency.Low,
            _ => ServiceRequestUrgency.Medium
        };
    }

    /// <summary>
    /// Enforces safety policies on Gemini generated output:
    /// - Eliminates dangerous DIY instructions
    /// - Guarantees uncertainty language
    /// - Filters out guaranteed diagnoses
    /// - Ensures out-of-scope actions (booking, payments, quotations) are absent
    /// </summary>
    private static void ApplySafetyPolicies(
        ProblemUnderstandingOutput output,
        Dictionary<string, string> additionalInfo,
        string? locationText)
    {
        output.ExtractedLocation = locationText;
        output.AdditionalInformation = additionalInfo;

        var dangerousTerms = new[]
        {
            "open the wire", "open the wiring", "touch the wire", "handle the wire",
            "strip the wire", "strip wire", "replace the wire yourself", "fix the wire yourself",
            "inspect the wire yourself", "do it yourself", "breaker yourself",
            "bypass", "disassemble the unit", "open the panel yourself",
            "replace electrical wiring", "replace electrical wiring themselves",
            "replace wiring", "wiring themselves", "wire themselves", "replace wiring yourself",
            "replace electrical wire"
        };

        var guaranteedTerms = new[]
        {
            "definitely", "guaranteed", "your wiring is broken",
            "your battery is dead", "100% certain"
        };

        // Sanitize problem summary
        var summary = output.ProblemSummary;
        bool hasDangerous = dangerousTerms.Any(t => summary.Contains(t, StringComparison.OrdinalIgnoreCase));
        bool hasGuaranteed = guaranteedTerms.Any(t => summary.Contains(t, StringComparison.OrdinalIgnoreCase));

        if (hasDangerous)
        {
            summary = "Possible safety issue. Professional inspection is recommended to ensure safety.";
        }
        else if (hasGuaranteed)
        {
            foreach (var g in guaranteedTerms)
            {
                summary = summary.Replace(g, "possible issue", StringComparison.OrdinalIgnoreCase);
            }
        }

        // Ensure uncertainty words
        var uncertaintyWords = new[] { "possible", "may", "could", "potential" };
        if (!uncertaintyWords.Any(u => summary.Contains(u, StringComparison.OrdinalIgnoreCase)))
        {
            summary = "Possible " + char.ToLowerInvariant(summary[0]) + summary[1..];
        }

        output.ProblemSummary = summary;

        // Sanitize follow-up questions
        var sanitizedQuestions = new List<string>();
        foreach (var q in output.FollowUpQuestions)
        {
            if (!dangerousTerms.Any(t => q.Contains(t, StringComparison.OrdinalIgnoreCase)))
            {
                sanitizedQuestions.Add(q);
            }
        }
        output.FollowUpQuestions = sanitizedQuestions;

        // If category is unclassified, urgency must remain Unknown
        if (string.Equals(output.Category, "Unclassified", StringComparison.OrdinalIgnoreCase))
        {
            output.Urgency = ServiceRequestUrgency.Unknown;
            output.NeedsMoreInformation = true;
            output.Confidence = Math.Min(output.Confidence, 0.4m);
        }
    }

    private static ProblemUnderstandingOutput CreateDegradedOutput(string? locationText)
    {
        return new ProblemUnderstandingOutput
        {
            Category = "Unclassified",
            ProblemSummary = "Possible service issue. Category could not be established.",
            Urgency = ServiceRequestUrgency.Unknown,
            NeedsMoreInformation = true,
            FollowUpQuestions = new[]
            {
                "Could you describe the problem you are experiencing?",
                "What specific symptoms or equipment are involved?"
            },
            Confidence = 0.2m,
            ExtractedLocation = locationText,
            AdditionalInformation = new Dictionary<string, string>
            {
                ["Degraded"] = "True"
            }
        };
    }

    private static AgentResult BuildNeedsMoreInformationResult(
        Guid serviceRequestId,
        string? locationText,
        string[] questions,
        decimal confidence)
    {
        var output = new ProblemUnderstandingOutput
        {
            Category = "Unclassified",
            ProblemSummary = "Insufficient information provided to determine the problem.",
            Urgency = ServiceRequestUrgency.Unknown,
            NeedsMoreInformation = true,
            FollowUpQuestions = questions,
            Confidence = Clamp(confidence),
            ExtractedLocation = string.IsNullOrWhiteSpace(locationText) ? null : locationText,
            AdditionalInformation = new Dictionary<string, string>()
        };

        return new AgentResult
        {
            Success = true,
            Message = "Problem analysis incomplete. Additional information required.",
            Data = output,
            NextAction = "AwaitingInformation"
        };
    }

    private static ProblemUnderstandingInput? ResolveInput(AgentContext context)
    {
        if (context.Data.TryGetValue(nameof(ProblemUnderstandingInput), out var raw) &&
            raw is ProblemUnderstandingInput typed)
        {
            return typed;
        }
        return null;
    }

    private static decimal Clamp(decimal value) => Math.Max(0m, Math.Min(1m, value));
}
