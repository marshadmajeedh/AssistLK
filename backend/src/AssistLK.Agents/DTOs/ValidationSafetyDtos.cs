using System.Text.Json.Serialization;

namespace AssistLK.Agents.DTOs;

// --- State Transition Validation DTOs ---

public class ValidationRequestDto
{
    [JsonPropertyName("job_id")]
    public string JobId { get; set; } = string.Empty;

    [JsonPropertyName("current_status")]
    public string CurrentStatus { get; set; } = string.Empty;

    [JsonPropertyName("target_status")]
    public string TargetStatus { get; set; } = string.Empty;

    [JsonPropertyName("elapsed_minutes")]
    public int ElapsedMinutes { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }
}

public class ValidationResponseDto
{
    [JsonPropertyName("is_valid")]
    public bool IsValid { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonPropertyName("risk_level")]
    public string RiskLevel { get; set; } = "Low";
}

// --- Safety & Sentiment Analysis DTOs ---

public class SentimentRequestDto
{
    [JsonPropertyName("feedback_text")]
    public string FeedbackText { get; set; } = string.Empty;
}

public class SentimentResponseDto
{
    [JsonPropertyName("score")]
    public float Score { get; set; }

    [JsonPropertyName("sentiment")]
    public string Sentiment { get; set; } = "Neutral";

    [JsonPropertyName("flagged_for_review")]
    public bool FlaggedForReview { get; set; }
}