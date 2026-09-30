using AssistLK.Domain.Entities;

namespace AssistLK.Application.Interfaces;

public interface IRegistrationChallengeRepository
{
    Task AddAsync(
        RegistrationChallenge challenge,
        CancellationToken cancellationToken = default);

    Task<RegistrationChallenge?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(
        RegistrationChallenge challenge,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}
