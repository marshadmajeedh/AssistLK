using AssistLK.Agents.Tools;

namespace AssistLK.Agents.Abstractions;

public interface IAgentTool
{
    string Name { get; }

    string Description { get; }

    Task<ToolResult> ExecuteAsync(
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default
    );
}
