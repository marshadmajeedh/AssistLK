using System.Text.Json.Serialization;
using AssistLK.Domain.Enums;

namespace AssistLK.Api.DTOs.ServiceRequests;

/// <summary>
/// Safe, structured problem understanding response exposed to API consumers.
/// Contains no internal reasoning chain, memory keys, or internal telemetry.
/// </summary>
public class ProblemUnderstandingResponseDto
{
    public Guid WorkflowId { get; set; }

    public Guid ExecutionId { get; set; }

    public Guid ServiceRequestId { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ServiceRequestStatus Status { get; set; }

    public string Category { get; set; } = string.Empty;

    public string ProblemSummary { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ServiceRequestUrgency Urgency { get; set; }

    public decimal Confidence { get; set; }

    public bool NeedsMoreInformation { get; set; }

    public IReadOnlyList<string> FollowUpQuestions { get; set; } = Array.Empty<string>();
}
