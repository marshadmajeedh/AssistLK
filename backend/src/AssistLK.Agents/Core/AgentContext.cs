namespace AssistLK.Agents.Core;

public class AgentContext
{
    public Guid WorkflowId { get; set; }

    public Guid? UserId { get; set; }

    public string Input { get; set; }
        = string.Empty;

    public Dictionary<string, object>
        Data { get; set; }
        = new();

    public Dictionary<string, string>
        Memory { get; set; }
        = new();
}
