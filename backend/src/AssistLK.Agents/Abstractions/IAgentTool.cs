namespace AssistLK.Agents.Abstractions;

public interface IAgentTool
{
    string Name { get; }

    Task<object> ExecuteAsync(
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default
    );
}
