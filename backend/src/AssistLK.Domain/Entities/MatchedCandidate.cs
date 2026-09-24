using AssistLK.Domain.Enums;

namespace AssistLK.Domain.Entities;

public class MatchedCandidate : BaseEntity
{
    public Guid MatchingExecutionId { get; set; }

    public MatchingExecution MatchingExecution { get; set; } = null!;

    public Guid ProviderId { get; set; }

    public ProviderProfile Provider { get; set; } = null!;

    public decimal Score { get; set; }

    public int Rank { get; set; }

    public decimal DistanceKm { get; set; }

    public string MatchRationale { get; set; } = string.Empty;

    public MatchedCandidateStatus Status { get; set; } = MatchedCandidateStatus.Recommended;
}