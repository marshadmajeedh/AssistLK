using AssistLK.Agents.Abstractions;
using AssistLK.Agents.Clients;
using AssistLK.Agents.DTOs;
using AssistLK.Agents.Tools;

namespace AssistLK.Agents.Core;

public class AssistantAgent : IAssistantAgent
{
    private readonly DemoProviderSearchTool _searchTool;
    private readonly IProblemUnderstandingClient _pythonAgentClient;

    public AssistantAgent(
        DemoProviderSearchTool searchTool, 
        IProblemUnderstandingClient pythonAgentClient)
    {
        _searchTool = searchTool;
        _pythonAgentClient = pythonAgentClient;
    }

    public async Task<string> ExecuteTaskAsync(string userPrompt)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            return "Please enter a valid prompt.";
        }

        // 1. Tool Trigger: Prompt එකේ "search" හෝ "find" තිබේ නම් Tool එක run කිරීම
        if (userPrompt.Contains("search", StringComparison.OrdinalIgnoreCase) || 
            userPrompt.Contains("find", StringComparison.OrdinalIgnoreCase))
        {
            var args = new Dictionary<string, object> { { "query", userPrompt } };
            var toolResult = await _searchTool.ExecuteAsync(args);
            return $"[Tool Output]: {toolResult.Data}\n\nAgent Response: I have found the relevant information for you!";
        }

        // 2. Python AI Agent Call (Exact Wire DTO Mapping)
        try
        {
            var requestDto = new AgentExecutionRequestDto
            {
                RequestId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                Input = new ProblemUnderstandingInputPayloadDto
                {
                    ServiceRequestId = Guid.NewGuid(),
                    Description = userPrompt
                }
            };

            var response = await _pythonAgentClient.ExecuteAsync(requestDto);
            
            if (response.Success && response.Result != null)
            {
                var summary = string.IsNullOrWhiteSpace(response.Result.ProblemSummary) 
                    ? response.Result.Category 
                    : response.Result.ProblemSummary;

                return $"[Python AI Agent]: {summary}";
            }

            return $"[Python AI Agent Error]: {response.ErrorMessage ?? "Execution failed"}";
        }
        catch (Exception ex)
        {
            // Python service එක down නම් Fallback response එක ලබාදීම
            return $"[AssistLK Fallback Agent]: I am ready to assist you with '{userPrompt}'. (Python Service Exception: {ex.Message})";
        }
    }
}