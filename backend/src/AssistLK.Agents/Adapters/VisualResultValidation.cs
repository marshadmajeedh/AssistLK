using System.Text.RegularExpressions;
using AssistLK.Agents.DTOs;
using AssistLK.Agents.Models;

namespace AssistLK.Agents.Adapters;

internal static class VisualResultValidation
{
    private static readonly Regex Payload = new("[A-Za-z0-9+/=_-]{80,}", RegexOptions.CultureInvariant);
    private static readonly string[] Forbidden = ["base64", "data:image", "inline_data", "inlinedata", "providerpayload",
        "chainofthought", "chain-of-thought", "chain of thought", "system instruction", "ignore previous instructions",
        "ignore all instructions", "readyformatching", "wiring is safe", "wire is safe", "no electrical danger",
        "guaranteed", "definitely", "ethnicity", "identified as", "person named", "face recognition"];

    public static VisualUnderstandingResult Validate(ProblemUnderstandingOutputPayloadDto result,
        IReadOnlyList<VisualEvidencePayloadDto> supplied)
    {
        var status = result.VisionStatus ?? (supplied.Count == 0 ? "not_requested" : "unsupported");
        var ids = result.AttachmentIdsUsed;
        var observations = result.VisualObservations;
        var limitations = result.VisualLimitations;
        if (status is not ("not_requested" or "used" or "unsupported" or "failed")
            || ids is null || observations is null || limitations is null
            || ids.Count > 3 || ids.Distinct().Count() != ids.Count || observations.Count > 5 || limitations.Count > 3
            || (supplied.Count == 0 && status != "not_requested") || (supplied.Count > 0 && status == "not_requested")
            || (status != "used" && (ids.Count > 0 || observations.Count > 0))
            || (status == "used" && (ids.Count == 0 || !ids.ToHashSet().SetEquals(supplied.Select(i => i.AttachmentId)))))
            throw new InvalidOperationException("Invalid structured visual result.");
        if (observations.Any(o => o is null || !ids.Contains(o.AttachmentId) || !SafeText(o.Observation, supplied, 240))
            || observations.GroupBy(o => o.AttachmentId).Any(group => group.Count() > 2)
            || limitations.Any(text => !SafeText(text, supplied, 240)))
            throw new InvalidOperationException("Invalid visual observation or limitation.");

        if (supplied.Count > 0)
        {
            if ((result.AdditionalInformation?.Count ?? 0) > 16 || (result.FollowUpQuestions?.Count ?? 0) > 3
                || !SafeText(result.Category, supplied, 40) || !SafeText(result.Urgency, supplied, 20))
                throw new InvalidOperationException("Invalid image-enhanced core result.");
            var text = new[] { result.ProblemSummary, result.ExtractedLocation ?? "location unavailable" }
                .Concat(result.FollowUpQuestions ?? []).Concat(result.AdditionalInformation?.Keys ?? Enumerable.Empty<string>())
                .Concat(result.AdditionalInformation?.Values ?? Enumerable.Empty<string>());
            if (text.Any(value => !SafeText(value, supplied, 1000)))
                throw new InvalidOperationException("Unsafe image-enhanced result content.");
        }
        return new VisualUnderstandingResult
        {
            VisionStatus = status, AttachmentIdsUsed = ids.ToArray(),
            VisualObservations = observations.ToArray(), VisualLimitations = limitations.ToArray()
        };
    }

    private static bool SafeText(string? text, IReadOnlyList<VisualEvidencePayloadDto> supplied, int maxLength)
        => !string.IsNullOrWhiteSpace(text) && text.Length <= maxLength && !Payload.IsMatch(text)
            && !Forbidden.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase))
            && !supplied.Any(image => text.Contains(image.DataBase64, StringComparison.Ordinal));
}
