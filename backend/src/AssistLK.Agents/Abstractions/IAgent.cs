using AssistLK.Agents.Core;
using AssistLK.Agents.Models;

namespace AssistLK.Agents.Abstractions;

public interface IAgent
{
    string Name { get; }

    Task<AgentResult> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default
    );
}