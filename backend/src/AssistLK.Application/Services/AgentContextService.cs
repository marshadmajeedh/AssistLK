using AssistLK.Agents.Core;

namespace AssistLK.Application.Services;

public class AgentContextService
{
    private readonly AgentMemoryService _memory;

    public AgentContextService(
        AgentMemoryService memory)
    {
        _memory = memory;
    }

    public async Task LoadMemoryAsync(
        AgentContext context)
    {
        var memories =
            await _memory.GetAllAsync(
                context.WorkflowId);

        foreach (var item in memories)
        {
            context.Memory[item.Key] =
                item.Value;
        }
    }
}
