using AssistLK.Agents.Abstractions;

namespace AssistLK.Agents.Tools;

public sealed record ProblemClassificationData(
    string Category,
    decimal Confidence,
    IReadOnlyList<string> SupportingTerms);

/// <summary>
/// Tool to classify a natural-language service problem description into canonical service categories.
/// No database access, provider matching, or external dependencies.
/// </summary>
public sealed class ProblemClassificationTool : IAgentTool
{
    public string Name => "ProblemClassificationTool";

    public string Description =>
        "Classifies a natural-language service problem description into canonical service categories.";

    private sealed record CategoryKeywords(
        string Category,
        string[] Keywords);

    private static readonly IReadOnlyList<CategoryKeywords> Categories = new[]
    {
        new CategoryKeywords("Plumbing", new[]
        {
            "pipe", "leak", "leaking", "plumbing", "tap", "drip", "dripping",
            "water", "drain", "flush", "toilet", "sink", "faucet",
            "burst", "flood", "flooding", "blocked", "clog"
        }),
        new CategoryKeywords("Electrical", new[]
        {
            "electric", "electrical", "socket", "plug", "wiring",
            "wire", "switch", "circuit", "breaker", "fuse", "power",
            "light", "lights", "outlet", "sparks", "spark", "shock",
            "tripped", "trip", "burning", "burn", "smoke"
        }),
        new CategoryKeywords("Vehicle Repair", new[]
        {
            "car", "vehicle", "engine", "battery", "tyre", "tire",
            "wheel", "brake", "brakes", "transmission", "ignition",
            "starter", "fuel", "exhaust", "overheating", "accident",
            "truck", "van", "motorcycle", "bike", "breakdown"
        }),
        new CategoryKeywords("Appliance Repair", new[]
        {
            "fridge", "refrigerator", "washing", "washer", "dryer",
            "dishwasher", "oven", "microwave", "freezer", "air conditioner",
            "ac", "heater", "fan", "appliance", "cooling", "heating"
        })
    };

    public Task<ToolResult> ExecuteAsync(
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        string description = string.Empty;
        if (parameters.TryGetValue("description", out var descObj) && descObj != null)
        {
            description = descObj.ToString() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return Task.FromResult(new ToolResult
            {
                Success = true,
                Message = "Description was empty. Returned Unclassified.",
                Data = new ProblemClassificationData(
                    Category: "Unclassified",
                    Confidence: 0.0m,
                    SupportingTerms: Array.Empty<string>())
            });
        }

        var lower = description.ToLowerInvariant();
        var tokens = Tokenise(lower);

        string bestCategory = "Unclassified";
        int bestScore = 0;
        List<string> bestTerms = new();

        foreach (var cat in Categories)
        {
            var matchedTerms = new List<string>();
            foreach (var kw in cat.Keywords)
            {
                if (kw.Contains(' '))
                {
                    if (lower.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedTerms.Add(kw);
                    }
                }
                else
                {
                    if (WordMatchesKeyword(tokens, kw))
                    {
                        matchedTerms.Add(kw);
                    }
                }
            }

            if (matchedTerms.Count > bestScore)
            {
                bestScore = matchedTerms.Count;
                bestCategory = cat.Category;
                bestTerms = matchedTerms;
            }
        }

        decimal confidence;
        if (bestScore == 0)
        {
            bestCategory = "Unclassified";
            confidence = 0.2m;
        }
        else
        {
            confidence = Math.Min(0.5m + (bestScore * 0.1m), 0.95m);
        }

        return Task.FromResult(new ToolResult
        {
            Success = true,
            Message = "Problem classified.",
            Data = new ProblemClassificationData(
                Category: bestCategory,
                Confidence: confidence,
                SupportingTerms: bestTerms)
        });
    }

    private static HashSet<string> Tokenise(string lower)
    {
        return new HashSet<string>(
            lower.Split(
                new[] { ' ', ',', '.', '!', '?', ';', ':', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries),
            StringComparer.OrdinalIgnoreCase);
    }

    private static bool WordMatchesKeyword(HashSet<string> words, string keyword)
    {
        if (words.Contains(keyword))
            return true;

        foreach (var word in words)
        {
            if (word.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
                return true;

            if (keyword.StartsWith(word, StringComparison.OrdinalIgnoreCase) && word.Length >= 4)
                return true;
        }

        return false;
    }
}
