using AssistLK.Agents.Abstractions;
using AssistLK.Agents.Core;
using AssistLK.Agents.Models;
using AssistLK.Domain.Enums;

namespace AssistLK.Agents.Agents;

/// <summary>
/// Component 1 – Problem Understanding Agent.
///
/// Responsibility: Determine what kind of help the customer actually needs
/// by analysing their natural-language service request and producing
/// a structured classification result.
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
/// Memory integration (Phase 7D) and tool integration (Phase 7D)
/// are not included in this phase.
/// </summary>
public sealed class ProblemUnderstandingAgent : IAgent
{
    // -------------------------------------------------------
    // Agent identity
    // -------------------------------------------------------

    public string Name => "ProblemUnderstandingAgent";

    // -------------------------------------------------------
    // Service category definitions
    // -------------------------------------------------------

    /// <summary>
    /// Represents a recognisable service category with indicative
    /// vocabulary. Kept private – not exposed as a public tool.
    /// </summary>
    private sealed record CategoryDefinition(
        string Category,
        string[] Keywords,
        string[] UrgentKeywords);

    private static readonly IReadOnlyList<CategoryDefinition> KnownCategories =
        new List<CategoryDefinition>
        {
            new CategoryDefinition(
                Category: "Plumbing",
                Keywords: new[]
                {
                    "pipe", "leak", "leaking", "plumbing", "tap", "drip",
                    "water", "drain", "flush", "toilet", "sink", "faucet",
                    "burst", "flood", "blocked", "clog"
                },
                UrgentKeywords: new[]
                {
                    "burst", "flood", "flooding", "gushing", "overflow",
                    "overflowing", "sewage"
                }),

            new CategoryDefinition(
                Category: "Electrical",
                Keywords: new[]
                {
                    "electric", "electrical", "socket", "plug", "wiring",
                    "wire", "switch", "circuit", "breaker", "fuse", "power",
                    "light", "lights", "outlet", "sparks", "spark", "shock",
                    "tripped", "trip"
                },
                UrgentKeywords: new[]
                {
                    "burning", "burn", "smoke", "sparks", "spark", "fire",
                    "shock", "shocked", "trip", "tripped", "smell", "smells",
                    "hot", "overheating", "overheat", "melting"
                }),

            new CategoryDefinition(
                Category: "Vehicle Repair",
                Keywords: new[]
                {
                    "car", "vehicle", "engine", "battery", "tyre", "tire",
                    "wheel", "brake", "brakes", "transmission", "ignition",
                    "starter", "fuel", "exhaust", "overheating", "accident",
                    "truck", "van", "motorcycle", "bike"
                },
                UrgentKeywords: new[]
                {
                    "accident", "crash", "road", "breakdown", "stopped",
                    "smoke", "fire", "brakes", "brake", "overheating"
                }),

            new CategoryDefinition(
                Category: "Appliance Repair",
                Keywords: new[]
                {
                    "fridge", "refrigerator", "washing", "washer", "dryer",
                    "dishwasher", "oven", "microwave", "freezer", "air conditioner",
                    "ac", "heater", "fan", "appliance", "machine", "cooling",
                    "heating", "not cooling", "not heating", "not working"
                },
                UrgentKeywords: new[]
                {
                    "fire", "smoke", "burning", "spark", "sparks",
                    "gas", "leak", "leaking"
                })
        };

    // -------------------------------------------------------
    // IAgent implementation
    // -------------------------------------------------------

    public Task<AgentResult> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        // Extract the typed input from AgentContext.Data if present;
        // fall back to the plain-text AgentContext.Input for compatibility
        // with the shared framework.
        var input = ResolveInput(context);

        var description = input?.Description
            ?? context.Input
            ?? string.Empty;

        // Gracefully handle empty or whitespace-only descriptions.
        if (string.IsNullOrWhiteSpace(description))
        {
            return Task.FromResult(BuildNeedsMoreInformationResult(
                serviceRequestId: input?.ServiceRequestId ?? Guid.Empty,
                locationText: input?.LocationText,
                questions: new[]
                {
                    "Could you describe the problem you are experiencing?"
                },
                confidence: 0m));
        }

        var output = Analyse(description, input);

        var result = new AgentResult
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

        return Task.FromResult(result);
    }

    // -------------------------------------------------------
    // Core analysis logic
    // -------------------------------------------------------

    private static ProblemUnderstandingOutput Analyse(
        string description,
        ProblemUnderstandingInput? input)
    {
        var lower = description.ToLowerInvariant();
        var words = Tokenise(lower);

        var matchedCategory = MatchCategory(words, lower);
        var hasCategory = matchedCategory is not null;

        var isUrgent = hasCategory
            && ContainsAny(words, lower, matchedCategory!.UrgentKeywords);

        // Determine whether we need more information.
        var needsMoreInfo = !hasCategory || IsAmbiguous(description, lower, hasCategory);

        var followUpQuestions = needsMoreInfo
            ? BuildFollowUpQuestions(description, lower, matchedCategory)
            : Array.Empty<string>();

        var confidence = CalculateConfidence(
            hasCategory: hasCategory,
            isUrgent: isUrgent,
            needsMoreInfo: needsMoreInfo,
            matchedCategory: matchedCategory,
            lower: lower);

        var urgency = DetermineUrgency(
            hasCategory: hasCategory,
            isUrgent: isUrgent,
            lower: lower,
            matchedCategory: matchedCategory,
            needsMoreInfo: needsMoreInfo);

        var summary = BuildProblemSummary(
            matchedCategory: matchedCategory,
            lower: lower,
            isUrgent: isUrgent);

        return new ProblemUnderstandingOutput
        {
            Category = matchedCategory?.Category ?? "Unclassified",
            ProblemSummary = summary,
            Urgency = urgency,
            NeedsMoreInformation = needsMoreInfo,
            FollowUpQuestions = followUpQuestions,
            Confidence = Clamp(confidence),
            ExtractedLocation = string.IsNullOrWhiteSpace(input?.LocationText)
                ? null
                : input.LocationText,
            AdditionalInformation = new Dictionary<string, string>()
        };
    }

    // -------------------------------------------------------
    // Category matching
    // -------------------------------------------------------

    private static CategoryDefinition? MatchCategory(
        HashSet<string> words,
        string lower)
    {
        CategoryDefinition? bestMatch = null;
        int bestScore = 0;

        foreach (var category in KnownCategories)
        {
            int score = CountMatches(words, lower, category.Keywords);
            if (score > bestScore)
            {
                bestScore = score;
                bestMatch = category;
            }
        }

        // Require at least one keyword match to classify.
        return bestScore > 0 ? bestMatch : null;
    }

    private static int CountMatches(
        HashSet<string> words,
        string lower,
        string[] keywords)
    {
        int count = 0;
        foreach (var kw in keywords)
        {
            if (kw.Contains(' '))
            {
                // Multi-word keyword: use substring matching.
                if (lower.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    count++;
            }
            else
            {
                // Use prefix matching to handle plurals and inflections:
                // "sockets" matches keyword "socket",
                // "leaking" matches keyword "leak".
                if (WordMatchesKeyword(words, kw))
                    count++;
            }
        }
        return count;
    }

    private static bool ContainsAny(
        HashSet<string> words,
        string lower,
        string[] terms)
    {
        foreach (var term in terms)
        {
            if (term.Contains(' '))
            {
                if (lower.Contains(term, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else
            {
                if (WordMatchesKeyword(words, term))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns true when any tokenised word from the input either
    /// exactly matches the keyword, or starts with the keyword
    /// (covering plurals: "sockets" → "socket"),
    /// or the keyword starts with the word (covering truncation).
    /// </summary>
    private static bool WordMatchesKeyword(
        HashSet<string> words,
        string keyword)
    {
        if (words.Contains(keyword))
            return true;

        foreach (var word in words)
        {
            // "sockets" starts with "socket" → match
            if (word.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
                return true;

            // keyword "smells" vs word "smell" — keyword starts with word
            if (keyword.StartsWith(word, StringComparison.OrdinalIgnoreCase) &&
                word.Length >= 4)
                return true;
        }

        return false;
    }

    // -------------------------------------------------------
    // Ambiguity detection
    // -------------------------------------------------------

    private static bool IsAmbiguous(
        string description,
        string lower,
        bool hasCategory)
    {
        if (!hasCategory)
            return true;

        // Very short descriptions (≤4 words) are likely too vague.
        var wordCount = description
            .Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (wordCount <= 4 && !lower.Contains("flood") && !lower.Contains("fire"))
            return true;

        // Lone generic words without problem description.
        var tooGeneric = new[]
        {
            "broken", "not working", "issue", "problem",
            "something wrong", "not ok"
        };
        if (ContainsAny(
                new HashSet<string>(description
                    .ToLowerInvariant()
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)),
                lower,
                tooGeneric)
            && wordCount <= 6)
        {
            return true;
        }

        return false;
    }

    // -------------------------------------------------------
    // Follow-up question generation
    // -------------------------------------------------------

    private static IReadOnlyList<string> BuildFollowUpQuestions(
        string description,
        string lower,
        CategoryDefinition? category)
    {
        var questions = new List<string>();

        if (category is null)
        {
            questions.Add(
                "Could you describe the appliance, system, or vehicle that is affected?");
            questions.Add(
                "What symptoms are you experiencing? For example, is there noise, a smell, or a visible issue?");
        }
        else
        {
            switch (category.Category)
            {
                case "Vehicle Repair":
                    questions.Add(
                        "What happens when you try to start the vehicle? For example, does it make any sound?");
                    if (!lower.Contains("warning") && !lower.Contains("light"))
                        questions.Add(
                            "Are any warning lights visible on the dashboard?");
                    break;

                case "Plumbing":
                    questions.Add(
                        "Which fixture or pipe is affected, and approximately how much water is leaking?");
                    break;

                case "Electrical":
                    questions.Add(
                        "Which circuit or area of the property is affected?");
                    break;

                case "Appliance Repair":
                    if (!lower.Contains("fridge") &&
                        !lower.Contains("washing") &&
                        !lower.Contains("oven") &&
                        !lower.Contains("microwave") &&
                        !lower.Contains("freezer") &&
                        !lower.Contains("dryer") &&
                        !lower.Contains("dishwasher") &&
                        !lower.Contains("air conditioner") &&
                        !lower.Contains("heater"))
                    {
                        questions.Add(
                            "What appliance is affected and what happens when you try to use it?");
                    }
                    else
                    {
                        questions.Add(
                            "How long has the appliance had this problem, and does it make any unusual sounds or smells?");
                    }
                    break;

                default:
                    questions.Add(
                        "Could you describe what you are experiencing in more detail?");
                    break;
            }
        }

        // Enforce the upper bound of 3 questions.
        return questions.Take(3).ToArray();
    }

    // -------------------------------------------------------
    // Urgency classification
    // -------------------------------------------------------

    private static ServiceRequestUrgency DetermineUrgency(
        bool hasCategory,
        bool isUrgent,
        string lower,
        CategoryDefinition? matchedCategory,
        bool needsMoreInfo)
    {
        if (!hasCategory)
            return ServiceRequestUrgency.Unknown;

        // Electrical safety hazard: immediate risk detected.
        if (matchedCategory?.Category == "Electrical" && isUrgent)
        {
            // Conservative: burning/sparks smell is at least High.
            // "Fire" would push it to Critical.
            if (lower.Contains("fire"))
                return ServiceRequestUrgency.Critical;

            return ServiceRequestUrgency.High;
        }

        // Flooding or burst pipe is High urgency.
        if (matchedCategory?.Category == "Plumbing")
        {
            if (lower.Contains("burst") ||
                lower.Contains("flood") ||
                lower.Contains("flooding") ||
                lower.Contains("overflow"))
                return ServiceRequestUrgency.High;

            if (lower.Contains("drip") || lower.Contains("dripping"))
                return ServiceRequestUrgency.Low;

            return ServiceRequestUrgency.Medium;
        }

        // Vehicle stopped on a road is High.
        if (matchedCategory?.Category == "Vehicle Repair")
        {
            if (lower.Contains("accident") || lower.Contains("crash"))
                return ServiceRequestUrgency.Critical;

            if (isUrgent ||
                lower.Contains("road") ||
                lower.Contains("breakdown") ||
                lower.Contains("stopped") ||
                lower.Contains("won't start") ||
                lower.Contains("will not start") ||
                lower.Contains("doesn't start") ||
                lower.Contains("does not start") ||
                lower.Contains("can't start") ||
                lower.Contains("cannot start"))
                return ServiceRequestUrgency.High;

            return ServiceRequestUrgency.Medium;
        }

        // Appliance.
        if (matchedCategory?.Category == "Appliance Repair")
        {
            if (isUrgent) // e.g. gas leak, fire
                return ServiceRequestUrgency.High;

            return ServiceRequestUrgency.Low;
        }

        return needsMoreInfo
            ? ServiceRequestUrgency.Unknown
            : ServiceRequestUrgency.Medium;
    }

    // -------------------------------------------------------
    // Problem summary generation
    // -------------------------------------------------------

    private static string BuildProblemSummary(
        CategoryDefinition? matchedCategory,
        string lower,
        bool isUrgent)
    {
        if (matchedCategory is null)
            return "Possible service issue. Category could not be determined from the available description.";

        return matchedCategory.Category switch
        {
            "Plumbing" when lower.Contains("burst") || lower.Contains("flood") =>
                "Possible burst pipe or water flooding. Immediate professional attention may be required.",

            "Plumbing" =>
                "Possible water leakage or plumbing fault.",

            "Electrical" when isUrgent =>
                "Possible electrical safety issue. Professional inspection is recommended.",

            "Electrical" =>
                "Possible electrical fault.",

            "Vehicle Repair" when lower.Contains("accident") || lower.Contains("crash") =>
                "Possible vehicle collision or serious mechanical failure.",

            "Vehicle Repair" when lower.Contains("start") ||
                                   lower.Contains("won't start") ||
                                   lower.Contains("doesn't start") =>
                "Possible battery or starting-system issue.",

            "Vehicle Repair" =>
                "Possible vehicle mechanical fault.",

            "Appliance Repair" when lower.Contains("fridge") || lower.Contains("refrigerator") || lower.Contains("freezer") =>
                "Possible refrigeration or cooling system fault.",

            "Appliance Repair" when lower.Contains("washing") || lower.Contains("washer") || lower.Contains("dryer") =>
                "Possible laundry appliance fault.",

            "Appliance Repair" =>
                "Possible appliance fault.",

            _ =>
                $"Possible {matchedCategory.Category.ToLowerInvariant()} issue."
        };
    }

    // -------------------------------------------------------
    // Confidence calculation
    // -------------------------------------------------------

    private static decimal CalculateConfidence(
        bool hasCategory,
        bool isUrgent,
        bool needsMoreInfo,
        CategoryDefinition? matchedCategory,
        string lower)
    {
        if (!hasCategory)
            return 0.2m;

        if (needsMoreInfo)
            return 0.4m;

        // A clear, detailed description in a known category.
        var baseConfidence = 0.75m;

        // Boost confidence when the description mentions specific symptoms.
        var specificSymptomTerms = new[]
        {
            "leaking", "dripping", "burst", "flooding",
            "not start", "won't start", "doesn't start",
            "burning", "sparks", "tripped",
            "not cooling", "not heating", "not working"
        };

        foreach (var term in specificSymptomTerms)
        {
            if (lower.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                baseConfidence = Math.Min(baseConfidence + 0.10m, 0.92m);
                break;
            }
        }

        return Clamp(baseConfidence);
    }

    // -------------------------------------------------------
    // Helper to build a "needs more information" result
    // -------------------------------------------------------

    private static AgentResult BuildNeedsMoreInformationResult(
        Guid serviceRequestId,
        string? locationText,
        string[] questions,
        decimal confidence)
    {
        var output = new ProblemUnderstandingOutput
        {
            Category = "Unclassified",
            ProblemSummary =
                "Insufficient information provided to determine the problem.",
            Urgency = ServiceRequestUrgency.Unknown,
            NeedsMoreInformation = true,
            FollowUpQuestions = questions,
            Confidence = Clamp(confidence),
            ExtractedLocation = string.IsNullOrWhiteSpace(locationText)
                ? null
                : locationText,
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

    // -------------------------------------------------------
    // Input resolution
    // -------------------------------------------------------

    private static ProblemUnderstandingInput? ResolveInput(AgentContext context)
    {
        if (context.Data.TryGetValue(
                nameof(ProblemUnderstandingInput),
                out var raw)
            && raw is ProblemUnderstandingInput typed)
        {
            return typed;
        }
        return null;
    }

    // -------------------------------------------------------
    // Tokenisation
    // -------------------------------------------------------

    private static HashSet<string> Tokenise(string lower)
    {
        return new HashSet<string>(
            lower.Split(
                new[] { ' ', ',', '.', '!', '?', ';', ':', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries),
            StringComparer.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------
    // Confidence clamping
    // -------------------------------------------------------

    private static decimal Clamp(decimal value)
        => Math.Max(0m, Math.Min(1m, value));
}
