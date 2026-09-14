using AssistLK.Domain.Enums;

namespace AssistLK.Domain.Entities;

public class MatchingExecution : BaseEntity
{
    public Guid ServiceRequestId { get; set; }

    public MatchingStrategy StrategyUsed { get; set; } = MatchingStrategy.AdaptiveProximity;

    public MatchingExecutionStatus Status { get; set; } = MatchingExecutionStatus.Pending;

    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

    public ICollection<MatchedCandidate> Candidates { get; set; } = new List<MatchedCandidate>();
}