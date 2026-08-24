using AssistLK.Agents.Core;

namespace AssistLK.Application.Services;

public class AgentExecutionService
{
    private readonly AgentWorkflowService _workflowService;

    private readonly AgentOrchestrator _orchestrator;

    public AgentExecutionService(
        AgentWorkflowService workflowService,
        AgentOrchestrator orchestrator)
    {
        _workflowService = workflowService;

        _orchestrator = orchestrator;
    }

    public async Task<object> ExecuteAsync(
        Guid? userId,
        string agentName,
        string input)
    {
        var workflow =
            await _workflowService.CreateAsync(
                userId,
                "AgentExecution",
                input
            );

        await _workflowService
            .SetStatusAsync(
                workflow.Id,
                "Running"
            );

        var execution =
            await _workflowService
            .StartExecutionAsync(
                workflow.Id,
                agentName,
                input
            );

        var context =
            new AgentContext
            {
                WorkflowId =
                    workflow.Id,

                UserId =
                    userId,

                Input =
                    input
            };

        var result =
            await _orchestrator.ExecuteAsync(
                agentName,
                context
            );

        await _workflowService
            .CompleteExecutionAsync(
                execution.Id,
                result.Success,
                result.Data
            );

        await _workflowService
            .SetStatusAsync(
                workflow.Id,
                result.Success
                    ? "Completed"
                    : "Failed"
            );

        return new
        {
            WorkflowId =
                workflow.Id,

            Result =
                result
        };
    }
}
