using System.Text.Json.Serialization;

namespace AssistLK.Agents.DTOs;

#region Request DTOs

/// <summary>
/// Top-level wire envelope sent to Python agent service (POST /agent/execute).
/// Matches app.schemas.request.AgentExecutionRequest.
/// </summary>
public class AgentExecutionRequestDto
{
    [JsonPropertyName("requestId")]
    public Guid RequestId { get; set; }

    [JsonPropertyName("agentName")]
    public string AgentName { get; set; } = "ProblemUnderstandingAgent";

    [JsonPropertyName("operation")]
    public string Operation { get; set; } = "analyze-problem";

    [JsonPropertyName("timestamp")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? Timestamp { get; set; }

    [JsonPropertyName("parameters")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Parameters { get; set; }

    [JsonPropertyName("input")]
    public ProblemUnderstandingInputPayloadDto Input { get; set; } = null!;
}

/// <summary>
/// Structured input payload matching app.schemas.request.ProblemUnderstandingInputDto.
/// </summary>
public class ProblemUnderstandingInputPayloadDto
{
    // Transient wire content: this DTO must never be passed to AgentWorkflowService.
    [JsonPropertyName("visualEvidence")]
    public IReadOnlyList<VisualEvidencePayloadDto> VisualEvidence { get; set; } = Array.Empty<VisualEvidencePayloadDto>();

    [JsonPropertyName("serviceRequestId")]
    public Guid ServiceRequestId { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("locationText")]
    public string? LocationText { get; set; }

    [JsonPropertyName("latitude")]
    public double? Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double? Longitude { get; set; }

    [JsonPropertyName("categoryHint")]
    public string? CategoryHint { get; set; }

    [JsonPropertyName("clarificationHistory")]
    public List<ClarificationHistoryItemPayloadDto> ClarificationHistory { get; set; } = new();
}

/// <summary>
/// Customer clarification entry matching app.schemas.request.ClarificationHistoryItemDto.
/// </summary>
public class ClarificationHistoryItemPayloadDto
{
    [JsonPropertyName("round")]
    public int Round { get; set; }

    [JsonPropertyName("question")]
    public string Question { get; set; } = string.Empty;

    [JsonPropertyName("answer")]
    public string Answer { get; set; } = string.Empty;
}

#endregion

#region Response DTOs

/// <summary>
/// Response wire envelope received from Python agent service.
/// Matches app.schemas.response.AgentExecutionResponse.
/// </summary>
public class AgentExecutionResponseDto
{
    [JsonPropertyName("requestId")]
    public Guid RequestId { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("result")]
    public ProblemUnderstandingOutputPayloadDto? Result { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("metadata")]
    public ExecutionMetadataPayloadDto? Metadata { get; set; }
}

/// <summary>
/// Authoritative result payload matching app.schemas.response.ProblemUnderstandingOutputDto.
/// </summary>
public class ProblemUnderstandingOutputPayloadDto
{
    [JsonPropertyName("category")]
    public string Category { get; set; } = "Unclassified";

    [JsonPropertyName("problemSummary")]
    public string ProblemSummary { get; set; } = string.Empty;

    [JsonPropertyName("urgency")]
    public string Urgency { get; set; } = "Unknown";

    [JsonPropertyName("needsMoreInformation")]
    public bool NeedsMoreInformation { get; set; }

    [JsonPropertyName("followUpQuestions")]
    public List<string> FollowUpQuestions { get; set; } = new();

    [JsonPropertyName("confidence")]
    public decimal Confidence { get; set; }

    [JsonPropertyName("extractedLocation")]
    public string? ExtractedLocation { get; set; }

    [JsonPropertyName("additionalInformation")]
    public Dictionary<string, string> AdditionalInformation { get; set; } = new();
}

/// <summary>
/// Safe execution audit metadata matching app.schemas.response.ExecutionMetadataDto.
/// </summary>
public class ExecutionMetadataPayloadDto
{
    [JsonPropertyName("agentName")]
    public string AgentName { get; set; } = "ProblemUnderstandingAgent";

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("degraded")]
    public bool Degraded { get; set; }

    [JsonPropertyName("durationMs")]
    public int DurationMs { get; set; }

    [JsonPropertyName("toolExecutions")]
    public List<ToolExecutionAuditPayloadDto> ToolExecutions { get; set; } = new();
}

/// <summary>
/// Safe tool audit summary matching app.schemas.response.ToolExecutionAuditDto.
/// </summary>
public class ToolExecutionAuditPayloadDto
{
    [JsonPropertyName("tool")]
    public string Tool { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("durationMs")]
    public int DurationMs { get; set; }
}

/// <summary>
/// Health check response matching GET /health.
/// </summary>
public class HealthCheckResponseDto
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("service")]
    public string? Service { get; set; }

    [JsonPropertyName("environment")]
    public string? Environment { get; set; }

    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }
}

#endregion
