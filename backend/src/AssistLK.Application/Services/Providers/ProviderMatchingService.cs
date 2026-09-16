using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace AssistLK.Application.Services.Providers
{
    // DTOs with proper null-safety initialization
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

    public class MatchResponse
    {
        [JsonPropertyName("thread_id")]
        public string ThreadId { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("recommended_candidate")]
        public object? RecommendedCandidate { get; set; }
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resume match thread {ThreadId} in Python microservice.", threadId);
                throw new ApplicationException("Could not process Admin match decision.", ex);
            }
        }
    }
}