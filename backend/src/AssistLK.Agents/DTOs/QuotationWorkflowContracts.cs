using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssistLK.Agents.DTOs;

public sealed class QuotationWorkflowStartRequest
{
    [JsonPropertyName("quotation_id")]
    public int QuotationId { get; set; }

    [JsonPropertyName("service_request_id")]
    public Guid? ServiceRequestId { get; set; }

    [JsonPropertyName("provider_id")]
    public Guid? ProviderId { get; set; }

    [JsonPropertyName("items")]
    public List<QuotationWorkflowItem> Items { get; set; } = new();

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

public sealed class QuotationWorkflowItem
{
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
}

public sealed class QuotationWorkflowResumeRequest
{
    [JsonPropertyName("thread_id")]
    public string ThreadId { get; set; } = string.Empty;

    [JsonPropertyName("decision")]
    public string Decision { get; set; } = string.Empty;

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }
}

public sealed class QuotationWorkflowStartResponse
{
    [JsonPropertyName("thread_id")]
    public string ThreadId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("approval_request")]
    public QuotationApprovalRequest? ApprovalRequest { get; set; }

    [JsonPropertyName("validation_errors")]
    public List<string> ValidationErrors { get; set; } = new();
}

public sealed class QuotationApprovalRequest
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("quotation_id")]
    public int QuotationId { get; set; }

    [JsonPropertyName("service_request_id")]
    public Guid? ServiceRequestId { get; set; }

    [JsonPropertyName("provider_id")]
    public Guid? ProviderId { get; set; }

    [JsonPropertyName("total_amount")]
    public decimal? TotalAmount { get; set; }

    [JsonPropertyName("allowed_actions")]
    public List<string> AllowedActions { get; set; } = new();

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("risk_assessment")]
    public QuotationRiskAssessment? RiskAssessment { get; set; }
}

public sealed class QuotationRiskAssessment
{
    [JsonPropertyName("risk_level")]
    public string? RiskLevel { get; set; }

    [JsonPropertyName("confidence")]
    public double? Confidence { get; set; }

    [JsonPropertyName("rationale")]
    public string? Rationale { get; set; }

    [JsonPropertyName("suggested_concerns")]
    public List<string>? SuggestedConcerns { get; set; }

    [JsonPropertyName("recommendation")]
    public string? Recommendation { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("prompt_tokens")]
    public int? PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int? CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int? TotalTokens { get; set; }
}

public sealed class QuotationWorkflowResumeResponse
{
    [JsonPropertyName("thread_id")]
    public string ThreadId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("final_state")]
    public QuotationWorkflowFinalState? FinalState { get; set; }
}

public sealed class QuotationWorkflowFinalState
{
    [JsonPropertyName("quotation_id")]
    public int? QuotationId { get; set; }

    [JsonPropertyName("customer_decision")]
    public string? CustomerDecision { get; set; }

    [JsonPropertyName("customer_remarks")]
    public string? CustomerRemarks { get; set; }

    [JsonPropertyName("final_message")]
    public string? FinalMessage { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalState { get; set; }
}