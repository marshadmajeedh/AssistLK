using AssistLK.Agents.Models;

namespace AssistLK.Agents.Core;


public class AgentSafetyPolicyEngine
{
    private readonly List<AgentSafetyRule>
        _rules = new()
        {
            new AgentSafetyRule
            {
                ActionType =
                "SEARCH_PROVIDER",

                RiskLevel =
                "LOW",

                RequiresApproval =
                false
            },


            new AgentSafetyRule
            {
                ActionType =
                "SEND_QUOTATION",

                RiskLevel =
                "MEDIUM",

                RequiresApproval =
                false
            },


            new AgentSafetyRule
            {
                ActionType =
                "CREATE_BOOKING",

                RiskLevel =
                "HIGH",

                RequiresApproval =
                true
            },


            new AgentSafetyRule
            {
                ActionType =
                "MAKE_PAYMENT",

                RiskLevel =
                "CRITICAL",

                RequiresApproval =
                true
            }
        };



    public AgentSafetyRule? Evaluate(
        string actionType)
    {
        return _rules.FirstOrDefault(
            x =>
            x.ActionType == actionType
        );
    }
}