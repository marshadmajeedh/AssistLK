using System.Net.Http.Json;
using AssistLK.Agents.DTOs;

namespace AssistLK.Agents.Core;

public class ValidationSafetyAgent
{
    private readonly HttpClient _httpClient;

    public ValidationSafetyAgent(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Validates job state transitions using Agent 4 rules engine.
    /// </summary>
    public async Task<ValidationResponseDto> ValidateTransitionAsync(ValidationRequestDto request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/agent/validate", request);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ValidationResponseDto>();
                return result ?? new ValidationResponseDto { IsValid = false, Reason = "Empty response" };
            }

            return new ValidationResponseDto 
            { 
                IsValid = false, 
                Reason = $"Safety agent HTTP error: {response.StatusCode}" 
            };
        }
        catch (Exception ex)
        {
            return new ValidationResponseDto 
            { 
                IsValid = false, 
                Reason = $"Safety check failed due to exception: {ex.Message}",
                RiskLevel = "High"
            };
        }
    }

    /// <summary>
    /// Analyzes feedback sentiment and safety risks using Agent 4.
    /// </summary>
    public async Task<SentimentResponseDto> AnalyzeSentimentAsync(SentimentRequestDto request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/agent/analyze-sentiment", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SentimentResponseDto>();
                return result ?? new SentimentResponseDto { Score = 0.5f, Sentiment = "Neutral", FlaggedForReview = false };
            }

            return new SentimentResponseDto 
            { 
                Score = 0.0f, 
                Sentiment = $"Safety agent HTTP error: {response.StatusCode}", 
                FlaggedForReview = true 
            };
        }
        catch (Exception ex)
        {
            return new SentimentResponseDto 
            { 
                Score = 0.0f, 
                Sentiment = $"Sentiment check failed due to exception: {ex.Message}", 
                FlaggedForReview = true 
            };
        }
    }
}