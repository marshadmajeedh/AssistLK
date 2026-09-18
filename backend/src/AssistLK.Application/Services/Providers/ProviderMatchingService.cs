using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace AssistLK.Application.Services.Providers
{
    public class ProviderCandidateDto
    {
        [JsonPropertyName("provider_id")]
        public string ProviderId { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("rating")]
        public double Rating { get; set; }

        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        [JsonPropertyName("verified")]
        public bool Verified { get; set; }

        [JsonPropertyName("skills")]
        public List<string> Skills { get; set; } = new();
        
        [JsonPropertyName("operating_radius_km")]
        public double OperatingRadiusKm { get; set; }
    }

    public class MatchStartRequest
    {
        [JsonPropertyName("objective")]
        public string Objective { get; set; } = string.Empty;

        [JsonPropertyName("urgency")]
        public int Urgency { get; set; }

        [JsonPropertyName("customer_latitude")]
        public double CustomerLatitude { get; set; }

        [JsonPropertyName("customer_longitude")]
        public double CustomerLongitude { get; set; }

        [JsonPropertyName("eligible_providers")]
        public List<ProviderCandidateDto> EligibleProviders { get; set; } = new();
    }

    public class MatchResumeRequest
    {
        [JsonPropertyName("thread_id")]
        public string ThreadId { get; set; } = string.Empty;

        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;

        [JsonPropertyName("admin_id")]
        public string AdminId { get; set; } = string.Empty;
    }

    public class RecommendedProvider
    {
        [JsonPropertyName("provider_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("score")]
        public decimal Score { get; set; }

        [JsonPropertyName("distance_km")]
        public decimal DistanceKm { get; set; }

        [JsonPropertyName("rating")]
        public decimal Rating { get; set; }

        [JsonPropertyName("verified")]
        public bool Verified { get; set; }

        [JsonPropertyName("match_rationale")]
        public string MatchRationale { get; set; } = string.Empty;

        [JsonPropertyName("rank")]
        public int Rank { get; set; }
    }

    public class TokenUsageDto
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

public class MatchResponse
{
    [JsonPropertyName("thread_id")]
    public string ThreadId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("recommended_provider")]
    public RecommendedProvider? RecommendedProvider { get; set; }

    [JsonPropertyName("tokens_consumed")]
    public TokenUsageDto? TokensConsumed { get; set; }

    [JsonPropertyName("final_outcome")]
    public object? FinalOutcome { get; set; }
}

    public interface IProviderMatchingService
    {
        Task<MatchResponse> StartMatchingAsync(MatchStartRequest request);
        Task<MatchResponse> ResumeMatchingAsync(string threadId, string action, string adminId);
    }

    public class ProviderMatchingService : IProviderMatchingService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ProviderMatchingService> _logger;

        public ProviderMatchingService(HttpClient httpClient, ILogger<ProviderMatchingService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<MatchResponse> StartMatchingAsync(MatchStartRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/match/start", request);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<MatchResponse>();
                return result ?? throw new InvalidOperationException("Empty response received from matching service.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error while starting match in Python microservice.");
                throw new ApplicationException("AI Matching Engine is currently unavailable.", ex);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout connecting to Python microservice.");
                throw new ApplicationException("AI Matching Engine timed out.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start match in Python microservice.");
                throw new ApplicationException("AI Matching Engine is unavailable.", ex);
            }
        }

        public async Task<MatchResponse> ResumeMatchingAsync(string threadId, string action, string adminId)
        {
            try
            {
                var request = new MatchResumeRequest
                {
                    ThreadId = threadId,
                    Action = action,
                    AdminId = adminId
                };

                var response = await _httpClient.PostAsJsonAsync("/match/resume", request);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<MatchResponse>();
                return result ?? throw new InvalidOperationException("Empty response received from matching service.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error resuming thread {ThreadId}.", threadId);
                throw new ApplicationException("Could not process Admin match decision (service unavailable).", ex);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout resuming thread {ThreadId}.", threadId);
                throw new ApplicationException("Admin decision timed out.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resume thread {ThreadId}.", threadId);
                throw new ApplicationException("Could not process Admin match decision.", ex);
            }
        }
    }
}