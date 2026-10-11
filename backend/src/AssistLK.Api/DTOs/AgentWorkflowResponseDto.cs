namespace AssistLK.Api.DTOs;

public class AgentWorkflowResponseDto
{
    public Guid WorkflowId { get; set; }

    public string Status { get; set; }
        = string.Empty;


    public object? Result { get; set; }
}