using AssistLK.Agents.Abstractions;
using AssistLK.Agents.Core;
using AssistLK.Agents.Models;

namespace AssistLK.Agents.Agents;


public class DemoProblemAgent : IAgent
{
    public string Name =>
        "DemoProblemAgent";


    public async Task<AgentResult> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {

        await Task.Delay(500, cancellationToken);


        var input =
            context.Input.ToLower();


        string problem;


        if(input.Contains("start"))
        {
            problem = "Possible battery or ignition issue";
        }
        else if(input.Contains("flat"))
        {
            problem = "Possible tyre issue";
        }
        else
        {
            problem = "General vehicle issue";
        }

        context.Memory["Issue"] =
            problem;

        return new AgentResult
        {
            Success = true,

            Message =
            "Problem analysis completed.",

            Data = new
            {
                Problem = problem,
                Confidence = 0.85
            },

            NextAction =
            "Find suitable service provider"
        };
    }
}