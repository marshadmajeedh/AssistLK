using AssistLK.Agents.Abstractions;

namespace AssistLK.Agents.Tools;

public class DemoProviderSearchTool
    : IAgentTool
{

    public string Name =>
        "ProviderSearchTool";

    public string Description =>
        "Find available service providers.";

    public async Task<ToolResult>
        ExecuteAsync(
            Dictionary<string,object> parameters,
            CancellationToken cancellationToken = default)
    {

        await Task.Delay(300,cancellationToken);

        return new ToolResult
        {
            Success = true,

            Message =
            "Providers found.",

            Data = new[]
            {
                new
                {
                    Name =
                    "ABC Vehicle Service",

                    Distance =
                    "2.5 km"
                },

                new
                {
                    Name =
                    "City Auto Garage",

                    Distance =
                    "4 km"
                }
            }
        };
    }
}
