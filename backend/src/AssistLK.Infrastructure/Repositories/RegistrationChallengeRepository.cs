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

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
