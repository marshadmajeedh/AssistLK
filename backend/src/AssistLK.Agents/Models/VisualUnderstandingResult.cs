using System.Text.Json.Serialization;

namespace AssistLK.Agents.Models;

/// <summary>Bounded semantic audit metadata only. No binary content or provider payload.</summary>
public sealed class VisualUnderstandingResult
{
    [JsonPropertyName("visionStatus")]
    public string VisionStatus { get; init; } = "not_requested";
    [JsonPropertyName("attachmentIdsUsed")]
    public IReadOnlyList<Guid> AttachmentIdsUsed { get; init; } = Array.Empty<Guid>();
    [JsonPropertyName("visualObservations")]
    public IReadOnlyList<VisualObservation> VisualObservations { get; init; } = Array.Empty<VisualObservation>();
    [JsonPropertyName("visualLimitations")]
    public IReadOnlyList<string> VisualLimitations { get; init; } = Array.Empty<string>();
}

public sealed class VisualObservation
{
    [JsonPropertyName("attachmentId")]
    public Guid AttachmentId { get; init; }
    [JsonPropertyName("observation")]
    public string Observation { get; init; } = string.Empty;
}
