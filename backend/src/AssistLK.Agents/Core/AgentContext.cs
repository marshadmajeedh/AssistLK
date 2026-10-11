namespace AssistLK.Agents.Core;

public class AgentContext
{
    // Request-scoped only, populated after the persistent execution input snapshot.
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<AssistLK.Agents.DTOs.VisualEvidencePayloadDto> VisualEvidence { get; set; }
        = Array.Empty<AssistLK.Agents.DTOs.VisualEvidencePayloadDto>();

    public Guid WorkflowId { get; set; }

    public Guid? UserId { get; set; }

    public string Input { get; set; }
        = string.Empty;

    public Dictionary<string, object>
        Data { get; set; }
        = new();

    public Dictionary<string, string>
        Memory { get; set; }
        = new();
}
