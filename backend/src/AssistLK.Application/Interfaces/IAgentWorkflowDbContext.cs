using AssistLK.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.Application.Interfaces;

public interface IAgentWorkflowDbContext
{
    DbSet<AgentWorkflow> AgentWorkflows { get; }

    DbSet<AgentExecution> AgentExecutions { get; }

    DbSet<AgentApproval> AgentApprovals { get; }

    DbSet<AgentAuditLog> AgentAuditLogs { get; }

    DbSet<AgentMemory> AgentMemories { get; }

    DbSet<AgentAction> AgentActions { get; }

    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);
}
