using AssistLK.Application.Interfaces;
using AssistLK.Domain.Entities;
using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.Infrastructure.Repositories;

public class RegistrationChallengeRepository : IRegistrationChallengeRepository
{
    private readonly AssistLKDbContext _context;

    public RegistrationChallengeRepository(AssistLKDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(RegistrationChallenge challenge, CancellationToken cancellationToken = default)
    {
        await _context.RegistrationChallenges.AddAsync(challenge, cancellationToken);
    }

    public async Task<RegistrationChallenge?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.RegistrationChallenges
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task RemoveAsync(RegistrationChallenge challenge, CancellationToken cancellationToken = default)
    {
        _context.RegistrationChallenges.Remove(challenge);
        return Task.CompletedTask;
    }

    public async Task<int> DeleteObsoleteChallengesAsync(DateTime obsoleteCutoffUtc, CancellationToken cancellationToken = default)
    {
        var obsolete = await _context.RegistrationChallenges
            .Where(c => c.ExpiresAtUtc <= obsoleteCutoffUtc || (c.IsConsumed && c.UpdatedAt <= obsoleteCutoffUtc))
            .ToListAsync(cancellationToken);

        if (obsolete.Count == 0)
        {
            return 0;
        }

        _context.RegistrationChallenges.RemoveRange(obsolete);
        await _context.SaveChangesAsync(cancellationToken);
        return obsolete.Count;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
