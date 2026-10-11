using AssistLK.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.Application.Interfaces;

public interface IProviderProfileDbContext
{
    DbSet<ProviderProfile> ProviderProfiles { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}