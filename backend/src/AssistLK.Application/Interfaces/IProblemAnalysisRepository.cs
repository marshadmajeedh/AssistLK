using AssistLK.Domain.Entities;

namespace AssistLK.Application.Interfaces;

public interface IProblemAnalysisRepository
{
    Task<IReadOnlyList<ProblemAnalysis>> GetByServiceRequestIdAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default);

    Task<ProblemAnalysis?> GetMostRecentByServiceRequestIdAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default);

    Task AddAsync(ProblemAnalysis problemAnalysis, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
