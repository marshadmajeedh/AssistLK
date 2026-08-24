namespace AssistLK.Agents.Tools;

public class ToolResult
{
    public bool Success { get; set; }

    public string Message { get; set; }
        = string.Empty;

    public object? Data { get; set; }
}
