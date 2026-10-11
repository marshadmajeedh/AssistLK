using AssistLK.Agents.Abstractions;

namespace AssistLK.Agents.Core;

public class AgentRegistry
{
    private readonly Dictionary<string, IAgent> _agents = new();

    public void Register(IAgent agent)
    {
        _agents[agent.Name] = agent;
    }

    public IAgent? Get(string name)
    {
        return _agents.TryGetValue(name, out var agent)
            ? agent
            : null;
    }
}
