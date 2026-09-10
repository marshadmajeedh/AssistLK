using AssistLK.Agents.Core;
using AssistLK.Application.Interfaces;
using AssistLK.Domain.Entities;

namespace AssistLK.Application.Services;

public class AgentSafetyService
{
    private readonly AgentSafetyPolicyEngine _engine;

    private readonly IAgentWorkflowDbContext _db;

    public AgentSafetyService(
        AgentSafetyPolicyEngine engine,
        IAgentWorkflowDbContext db)
    {
        _engine = engine;
        _db = db;
    }

    public async Task<AgentAction>
        CheckActionAsync(
            Guid workflowId,
            string actionType,
            string description)
    {

        var rule =
            _engine.Evaluate(actionType);

        var action =
            new AgentAction
            {
                Id = Guid.NewGuid(),

                WorkflowId =
                    workflowId,

                ActionType =
                    actionType,

                Description =
                    description,

                RiskLevel =
                    rule?.RiskLevel
                    ?? "UNKNOWN",

                RequiresApproval =
                    rule?.RequiresApproval
                    ?? true,

                Status =
                    "Pending",

                CreatedAtUtc =
                    DateTime.UtcNow
            };

        _db.AgentActions.Add(action);

        await _db.SaveChangesAsync();

        return action;
    }
}