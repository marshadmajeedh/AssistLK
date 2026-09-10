namespace AssistLK.Agents.Models;

public class AgentSafetyRule
{
    public string ActionType { get; set; }
        = string.Empty;

    public string RiskLevel { get; set; }
        = string.Empty;

    public bool RequiresApproval { get; set; }
}