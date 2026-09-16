using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace AssistLK.Application.Services.Providers
{
    public interface IProviderMatchingService
    {
        Task<MatchStartResponse> StartMatchingAsync(string objective);
        Task<MatchResumeResponse> ResumeMatchingAsync(string threadId, string action, string adminId);
    }

    public class MatchStartRequest
    {
        [JsonPropertyName("objective")]
        public string Objective { get; set; }
    }

    public class MatchStartResponse
    {
        [JsonPropertyName("thread_id")]
        public string ThreadId { get; set; }
        [JsonPropertyName("status")]
        public string Status { get; set; }
        [JsonPropertyName("recommended_provider")]
        public object RecommendedProvider { get; set; }
    }

    public class MatchResumeRequest
    {
        [JsonPropertyName("thread_id")]
        public string ThreadId { get; set; }
        [JsonPropertyName("action")]
        public string Action { get; set; } // "Approve" or "Reject"
        [JsonPropertyName("admin_id")]
        public string AdminId { get; set; }
    }

    public class MatchResumeResponse
    {
        [JsonPropertyName("thread_id")]
        public string ThreadId { get; set; }
        [JsonPropertyName("status")]
        public string Status { get; set; }
        [JsonPropertyName("final_outcome")]
        public object FinalOutcome { get; set; }
    }

    public class ProviderMatchingService : IProviderMatchingService
    {
        private readonly HttpClient _httpClient;

        public ProviderMatchingService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<MatchStartResponse> StartMatchingAsync(string objective)
        {
            var request = new MatchStartRequest { Objective = objective };
            var response = await _httpClient.PostAsJsonAsync("/match/start", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<MatchStartResponse>();
        }

        public async Task<MatchResumeResponse> ResumeMatchingAsync(string threadId, string action, string adminId)
        {
            var request = new MatchResumeRequest 
            { 
                ThreadId = threadId, 
                Action = action, 
                AdminId = adminId 
            };
            var response = await _httpClient.PostAsJsonAsync("/match/resume", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<MatchResumeResponse>();
        }
    }
}
