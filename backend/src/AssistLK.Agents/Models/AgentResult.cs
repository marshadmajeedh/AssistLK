namespace AssistLK.Agents.Models;

public class AgentResult
{
    public bool Success { get; set; }

    public string Message { get; set; }
        = string.Empty;

    public object? Data { get; set; }

    public string? NextAction { get; set; }
}