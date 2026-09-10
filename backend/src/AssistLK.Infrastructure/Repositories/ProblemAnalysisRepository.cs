using AssistLK.Application.Interfaces;
using AssistLK.Domain.Entities;
using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.Infrastructure.Repositories;

public class ProblemAnalysisRepository : IProblemAnalysisRepository
{
    private readonly AssistLKDbContext _context;

    public ProblemAnalysisRepository(AssistLKDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ProblemAnalysis>> GetByServiceRequestIdAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ProblemAnalyses
            .Where(x => x.ServiceRequestId == serviceRequestId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProblemAnalysis?> GetMostRecentByServiceRequestIdAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ProblemAnalyses
            .Where(x => x.ServiceRequestId == serviceRequestId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(
        ProblemAnalysis problemAnalysis,
        CancellationToken cancellationToken = default)
    {
        await _context.ProblemAnalyses.AddAsync(problemAnalysis, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
