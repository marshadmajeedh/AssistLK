using AssistLK.Agents.Core;

namespace AssistLK.Application.Services;

public class AgentExecutionService
{
    private readonly AgentWorkflowService _workflowService;

    private readonly AgentOrchestrator _orchestrator;

    private readonly AgentContextService _contextService;

    private readonly AgentMonitoringService _monitoringService;

    public AgentExecutionService(
        AgentWorkflowService workflowService,
        AgentOrchestrator orchestrator,
        AgentContextService contextService,
        AgentMonitoringService monitoringService)
    {
        _workflowService = workflowService;

        _orchestrator = orchestrator;

        _contextService = contextService;

        _monitoringService = monitoringService;
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

                await _contextService
                    .LoadMemoryAsync(context);

        var startTime =
            DateTime.UtcNow;

        var result =
            await _orchestrator.ExecuteAsync(
                agentName,
                context
            );

        var executionTime =
            (DateTime.UtcNow - startTime)
            .TotalMilliseconds;

        await _monitoringService.RecordAsync(
            workflow.Id,
            execution.Id,
            agentName,
            result.Success
                ? "Completed"
                : "Failed",
            (long)executionTime,
            0
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
