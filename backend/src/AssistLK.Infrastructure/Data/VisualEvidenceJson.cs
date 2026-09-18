using System.Text.Json;
using AssistLK.Domain.Entities;

namespace AssistLK.Infrastructure.Data;

internal static class VisualEvidenceJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public static string Write(ProblemAnalysisVisualEvidence value) => JsonSerializer.Serialize(value, Options);
    public static ProblemAnalysisVisualEvidence Read(string value) =>
        JsonSerializer.Deserialize<ProblemAnalysisVisualEvidence>(value, Options) ?? new();
}
