using System.Text.RegularExpressions;

namespace AssistLK.Domain.Entities;

/// <summary>Provider-neutral, bounded evidence for one accepted analysis revision.</summary>
public sealed class ProblemAnalysisVisualEvidence
{
    public string VisionStatus { get; init; } = "not_requested";
    public IReadOnlyList<Guid> AttachmentIdsUsed { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<ProblemAnalysisVisualObservation> Observations { get; init; } = Array.Empty<ProblemAnalysisVisualObservation>();
    public IReadOnlyList<string> Limitations { get; init; } = Array.Empty<string>();

    public ProblemAnalysisVisualEvidence ValidateFor(IReadOnlyList<Guid> supplied, IEnumerable<Guid> owned)
    {
        if (VisionStatus is not ("not_requested" or "used" or "unsupported" or "failed")
            || AttachmentIdsUsed is null || Observations is null || Limitations is null
            || supplied.Count > 3 || supplied.Distinct().Count() != supplied.Count
            || supplied.Any(id => id == Guid.Empty || !owned.Contains(id))
            || AttachmentIdsUsed.Count > 3 || AttachmentIdsUsed.Distinct().Count() != AttachmentIdsUsed.Count
            || Observations.Count > 5 || Limitations.Count > 3
            || (supplied.Count == 0 && VisionStatus != "not_requested")
            || (supplied.Count > 0 && VisionStatus == "not_requested")
            || (VisionStatus == "used" && (AttachmentIdsUsed.Count == 0 || !AttachmentIdsUsed.ToHashSet().SetEquals(supplied)))
            || (VisionStatus != "used" && (AttachmentIdsUsed.Count > 0 || Observations.Count > 0)))
            throw new ArgumentException("Invalid visual evidence for the analysis.");
        if (Observations.Any(o => o is null || !AttachmentIdsUsed.Contains(o.AttachmentId) || !SafeText(o.Observation))
            || Observations.GroupBy(o => o.AttachmentId).Any(g => g.Count() > 2)
            || Limitations.Any(text => !SafeText(text)))
            throw new ArgumentException("Invalid visual observation or limitation.");
        return new ProblemAnalysisVisualEvidence
        {
            VisionStatus = VisionStatus,
            AttachmentIdsUsed = VisionStatus == "used" ? supplied.ToArray() : Array.Empty<Guid>(),
            Observations = Observations.Select(o => new ProblemAnalysisVisualObservation
                { AttachmentId = o.AttachmentId, Observation = o.Observation.Trim() }).ToArray(),
            Limitations = Limitations.Select(s => s.Trim()).ToArray()
        };
    }

    private static bool SafeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 240 || Regex.IsMatch(text, "[A-Za-z0-9+/=_-]{80,}")) return false;
        string[] forbidden = ["base64", "data:image", "inline_data", "image_url", "providerpayload", "chainofthought",
            "chain-of-thought", "chain of thought", "system instruction", "ignore previous instructions",
            "ignore all instructions", "readyformatching", "wiring is safe", "no electrical danger", "file://", "http://", "https://",
            "open the wire", "touch the wire", "disassemble the unit", "guaranteed", "definitely", "ethnicity", "face recognition"];
        return !forbidden.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class ProblemAnalysisVisualObservation
{
    public Guid AttachmentId { get; init; }
    public string Observation { get; init; } = string.Empty;
}
