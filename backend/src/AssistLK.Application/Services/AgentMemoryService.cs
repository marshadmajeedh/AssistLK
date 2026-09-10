using AssistLK.Application.Interfaces;
using AssistLK.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.Application.Services;

public class AgentMemoryService
{
    private readonly IAgentWorkflowDbContext _db;

    public AgentMemoryService(
        IAgentWorkflowDbContext db)
    {
        _db = db;
    }

    public async Task SaveAsync(
        Guid workflowId,
        string key,
        string value,
        string sourceAgent)
    {
        var existing =
            await _db.AgentMemories
            .FirstOrDefaultAsync(x =>
                x.WorkflowId == workflowId &&
                x.Key == key);

        if (existing != null)
        {
            existing.Value = value;

            existing.SourceAgent =
                sourceAgent;

            existing.UpdatedAtUtc =
                DateTime.UtcNow;
        }
        else
        {
            _db.AgentMemories.Add(
                new AgentMemory
                {
                    Id = Guid.NewGuid(),

                    WorkflowId = workflowId,

                    Key = key,

                    Value = value,

                    SourceAgent =
                        sourceAgent,

                    CreatedAtUtc =
                        DateTime.UtcNow,

                    UpdatedAtUtc =
                        DateTime.UtcNow
                }
            );
        }

        await _db.SaveChangesAsync();
    }

    public async Task<string?> GetAsync(
        Guid workflowId,
        string key)
    {
        var memory =
            await _db.AgentMemories
            .FirstOrDefaultAsync(x =>
                x.WorkflowId == workflowId &&
                x.Key == key);

        return memory?.Value;
    }

    public async Task<List<AgentMemory>>
        GetAllAsync(
            Guid workflowId)
    {
        return await _db.AgentMemories
            .Where(x =>
                x.WorkflowId == workflowId)
            .ToListAsync();
    }
}
