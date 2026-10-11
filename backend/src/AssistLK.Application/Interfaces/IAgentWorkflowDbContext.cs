using AssistLK.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.Application.Interfaces;

public interface IAgentWorkflowDbContext
{
    DbSet<AgentWorkflow> AgentWorkflows { get; }

    DbSet<AgentExecution> AgentExecutions { get; }

    DbSet<AgentExecutionMetric> AgentExecutionMetrics { get; }

    DbSet<AgentApproval> AgentApprovals { get; }

    DbSet<AgentAuditLog> AgentAuditLogs { get; }

    DbSet<AgentMemory> AgentMemories { get; }

    DbSet<AgentAction> AgentActions { get; }

    DbSet<ProviderProfile> ProviderProfiles { get; }

    DbSet<ProviderSkill> ProviderSkills { get; }

    DbSet<ProviderLocation> ProviderLocations { get; }

    DbSet<ProviderAvailability> ProviderAvailabilities { get; }

    DbSet<MatchingExecution> MatchingExecutions { get; }

    DbSet<MatchedCandidate> MatchedCandidates { get; }

    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);
}
