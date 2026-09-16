using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace AssistLK.Application.Services.Providers
{
    public class MatchStartRequest
    {
        [JsonPropertyName("objective")]
        public string Objective { get; set; } = string.Empty;
    }

    public class MatchResumeRequest
    {
        [JsonPropertyName("thread_id")]
        public string ThreadId { get; set; } = string.Empty;

        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty; // "Approve" or "Reject"

        [JsonPropertyName("admin_id")]
        public string AdminId { get; set; } = string.Empty;
    }

    public class RecommendedProvider
    {
        [JsonPropertyName("id")]
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

    public class MatchResponse
    {
        [JsonPropertyName("thread_id")]
        public string ThreadId { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("recommended_provider")]
        public RecommendedProvider? RecommendedProvider { get; set; }

        [JsonPropertyName("final_outcome")]
        public object? FinalOutcome { get; set; }
    }

    public interface IProviderMatchingService
    {
        Task<MatchResponse> StartMatchingAsync(string objective);
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

        public async Task<MatchResponse> StartMatchingAsync(string objective)
        {
            try
            {
                var request = new MatchStartRequest { Objective = objective };
                var response = await _httpClient.PostAsJsonAsync("/match/start", request);

                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<MatchResponse>();

                return result ?? throw new InvalidOperationException("Empty response received from matching service.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP Request error while starting matching process in Python microservice.");
                throw new ApplicationException("AI Matching Engine is currently unavailable.", ex);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout while starting matching process in Python microservice.");
                throw new ApplicationException("AI Matching Engine is currently unavailable (timeout).", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start matching process in Python microservice.");
                throw new ApplicationException("AI Matching Engine is currently unavailable.", ex);
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
                _logger.LogError(ex, "HTTP Request error while resuming match thread {ThreadId} in Python microservice.", threadId);
                throw new ApplicationException("Could not process Admin match decision (service unavailable).", ex);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout while resuming match thread {ThreadId} in Python microservice.", threadId);
                throw new ApplicationException("Could not process Admin match decision (timeout).", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resume match thread {ThreadId} in Python microservice.", threadId);
                throw new ApplicationException("Could not process Admin match decision.", ex);
            }
        }
    }
}