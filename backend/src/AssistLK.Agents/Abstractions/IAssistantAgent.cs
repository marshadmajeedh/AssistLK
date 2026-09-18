namespace AssistLK.Agents.Abstractions;

public interface IAssistantAgent
{
    Task<string> ExecuteTaskAsync(string userPrompt);
}