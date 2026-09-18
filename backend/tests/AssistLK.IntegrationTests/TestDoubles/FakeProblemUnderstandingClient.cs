using System.Text.RegularExpressions;
using AssistLK.Agents.Clients;
using AssistLK.Agents.DTOs;

namespace AssistLK.IntegrationTests.TestDoubles;

/// <summary>
/// Deterministic in-memory fake for IProblemUnderstandingClient.
/// Eliminates network calls to Python FastAPI during automated integration test runs while
/// strictly exercising the real ExternalProblemUnderstandingAgentAdapter and ASP.NET Core stack.
/// </summary>
public class FakeProblemUnderstandingClient : IProblemUnderstandingClient
{
    private static readonly (string Category, string[] Keywords)[] CanonicalCategories = new[]
    {
        ("Plumbing", new[] { "pipe", "leak", "leaking", "plumbing", "tap", "drip", "dripping", "water", "drain", "flush", "toilet", "sink", "faucet", "burst", "flood", "flooding", "blocked", "clog" }),
        ("Electrical", new[] { "electric", "electrical", "socket", "plug", "wiring", "wire", "switch", "circuit", "breaker", "fuse", "power", "light", "lights", "outlet", "sparks", "spark", "sparking", "shock", "tripped", "trip", "burning", "burn", "smoke", "smoking" }),
        ("Vehicle Repair", new[] { "car", "vehicle", "engine", "battery", "tyre", "tire", "wheel", "brake", "brakes", "transmission", "ignition", "starter", "fuel", "exhaust", "overheating", "accident", "truck", "van", "motorcycle", "bike", "breakdown" }),
        ("Appliance Repair", new[] { "fridge", "refrigerator", "washing", "washer", "dryer", "dishwasher", "oven", "microwave", "freezer", "air conditioner", "ac", "heater", "fan", "appliance", "cooling", "heating", "compressor", "buzzing", "warm" })
    };

    public Func<AgentExecutionRequestDto, AgentExecutionResponseDto>? CustomHandler { get; set; }

    public Task<HealthCheckResponseDto?> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<HealthCheckResponseDto?>(new HealthCheckResponseDto
        {
            Status = "healthy",
            Service = "problem-understanding-agent-fake",
            Environment = "test",
            Provider = "offline",
            Model = "simulation"
        });
    }

    public Task<AgentExecutionResponseDto> ExecuteAsync(
        AgentExecutionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (CustomHandler != null)
        {
            return Task.FromResult(CustomHandler(request));
        }

        var input = request.Input;
        var desc = input?.Description ?? string.Empty;
        var trimmed = desc.Trim();

        // 1. Clarification History supplied and answered -> Resolve to clear analyzed result
        if (input?.ClarificationHistory != null && input.ClarificationHistory.Any(c => !string.IsNullOrWhiteSpace(c.Answer)))
        {
            var category = ResolveClarifiedCategory(trimmed, input.CategoryHint);
            return Task.FromResult(CreateSuccessResponse(
                request.RequestId,
                category,
                $"Clarified {category.ToLowerInvariant()} issue based on customer feedback.",
                "High",
                false,
                new List<string>(),
                0.88m));
        }

        // 2. Ambiguous requests -> Return AwaitingInformation with follow-up questions
        if (IsAmbiguous(trimmed))
        {
            var category = trimmed.Contains("refrigerator", StringComparison.OrdinalIgnoreCase) ||
                           trimmed.Contains("fridge", StringComparison.OrdinalIgnoreCase)
                ? "Appliance Repair"
                : "Unclassified";

            var summary = category == "Appliance Repair"
                ? "Possible appliance fault. Further information needed to confirm issue."
                : "Insufficient information provided to diagnose issue.";

            var questions = category == "Appliance Repair"
                ? new List<string> { "Could you describe what happens when you turn on the appliance?" }
                : new List<string> { "Could you describe the problem in more detail?" };

            return Task.FromResult(CreateSuccessResponse(
                request.RequestId,
                category,
                summary,
                "Unknown",
                true,
                questions,
                0.35m));
        }

        // 3. Clear deterministic classification based on description
        var clearCategory = ResolveCategory(trimmed, input?.CategoryHint);
        return Task.FromResult(CreateSuccessResponse(
            request.RequestId,
            clearCategory,
            $"Identified {clearCategory.ToLowerInvariant()} service request.",
            "High",
            false,
            new List<string>(),
            0.90m));
    }

    private static bool IsAmbiguous(string desc)
    {
        if (string.Equals(desc, "refrigerator", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(desc, "refrigerator not working", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(desc, "fridge not working", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(desc, "my refrigerator is broken", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(desc, "Machine broken", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(desc, "Something broken please help", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(desc, "Something is broken and needs urgent help.", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(desc, "broken", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var cat = ResolveCategory(desc, null);
        return cat == "Unclassified";
    }

    private static string ResolveCategory(string desc, string? categoryHint)
    {
        var lower = desc.ToLowerInvariant();
        string bestCategory = "Unclassified";
        int maxMatches = 0;

        foreach (var (category, keywords) in CanonicalCategories)
        {
            int matches = keywords.Count(kw => Regex.IsMatch(lower, $@"\b{Regex.Escape(kw)}\b"));
            if (matches > maxMatches)
            {
                bestCategory = category;
                maxMatches = matches;
            }
        }

        if (maxMatches > 0)
        {
            return bestCategory;
        }

        if (!string.IsNullOrWhiteSpace(categoryHint) && IsAllowedCategory(categoryHint))
        {
            return categoryHint.Trim();
        }

        return "Unclassified";
    }

    private static string ResolveClarifiedCategory(string desc, string? categoryHint)
    {
        var category = ResolveCategory(desc, categoryHint);
        if (category == "Unclassified")
        {
            return !string.IsNullOrWhiteSpace(categoryHint) && IsAllowedCategory(categoryHint)
                ? categoryHint.Trim()
                : "Appliance Repair";
        }
        return category;
    }

    private static bool IsAllowedCategory(string category)
    {
        return category.Equals("Plumbing", StringComparison.OrdinalIgnoreCase) ||
               category.Equals("Electrical", StringComparison.OrdinalIgnoreCase) ||
               category.Equals("Vehicle Repair", StringComparison.OrdinalIgnoreCase) ||
               category.Equals("Appliance Repair", StringComparison.OrdinalIgnoreCase) ||
               category.Equals("Unclassified", StringComparison.OrdinalIgnoreCase);
    }

    private static AgentExecutionResponseDto CreateSuccessResponse(
        Guid requestId,
        string category,
        string summary,
        string urgency,
        bool needsMoreInfo,
        List<string> questions,
        decimal confidence)
    {
        return new AgentExecutionResponseDto
        {
            RequestId = requestId,
            Success = true,
            Result = new ProblemUnderstandingOutputPayloadDto
            {
                Category = category,
                ProblemSummary = summary,
                Urgency = urgency,
                NeedsMoreInformation = needsMoreInfo,
                FollowUpQuestions = questions,
                Confidence = confidence,
                AdditionalInformation = new Dictionary<string, string>
                {
                    ["Provider"] = "gemini",
                    ["Simulated"] = "True"
                }
            },
            Metadata = new ExecutionMetadataPayloadDto
            {
                AgentName = "ProblemUnderstandingAgent",
                Provider = "gemini",
                DurationMs = 15,
                Degraded = false,
                ToolExecutions = new List<ToolExecutionAuditPayloadDto>
                {
                    new() { Tool = "LocationExtractionTool", Success = true, DurationMs = 2 },
                    new() { Tool = "ProblemClassificationTool", Success = true, DurationMs = 2 },
                    new() { Tool = "ServiceKnowledgeTool", Success = true, DurationMs = 2 }
                }
            }
        };
    }
}
