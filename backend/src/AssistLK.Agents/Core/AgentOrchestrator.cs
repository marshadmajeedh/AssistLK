using AssistLK.Agents.Models;

namespace AssistLK.Agents.Core;

public class AgentOrchestrator
{
    private readonly AgentRegistry _registry;

    public AgentOrchestrator(
        AgentRegistry registry)
    {
        _registry = registry;
    }

    public async Task<AgentResult>
        RunAsync(
            string agentName,
            AgentContext context)
    {
        var agent =
            _registry.Get(agentName);

        if (agent == null)
        {
            return new AgentResult
            {
                Success = false,
                Message =
                    $"Agent {agentName} not found"
            };
        }

        return await agent.ExecuteAsync(
            context);
    }
}