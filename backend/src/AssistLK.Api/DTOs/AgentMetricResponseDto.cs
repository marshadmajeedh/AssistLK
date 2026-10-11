namespace AssistLK.Api.DTOs;

public class AgentMetricResponseDto
{
    public string AgentName { get; set; }
        = string.Empty;

    public string Status { get; set; }
        = string.Empty;

    public long ExecutionTimeMs { get; set; }

    public int ToolCalls { get; set; }
}