using AssistLK.Agents.Tools;

namespace AssistLK.Agents.Core;

public class ToolExecutor
{
    private readonly ToolRegistry _registry;

    public ToolExecutor(
        ToolRegistry registry)
    {
        _registry = registry;
    }

    public async Task<ToolResult> ExecuteAsync(
        string toolName,
        Dictionary<string,object> parameters)
    {

        var tool =
            _registry.Get(toolName);

        if(tool == null)
        {
            return new ToolResult
            {
                Success = false,

                Message =
                $"Tool '{toolName}' not found.",

                ErrorCode =
                "TOOL_NOT_FOUND"
            };
        }

        return await tool.ExecuteAsync(
            parameters
        );
    }
}
