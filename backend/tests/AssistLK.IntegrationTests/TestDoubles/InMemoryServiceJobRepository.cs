using AssistLK.Application.Interfaces;

namespace AssistLK.IntegrationTests.TestDoubles;

public sealed class InMemoryServiceJobRepository : IServiceJobRepository
{
    public Task<Guid?> GetIdByServiceRequestIdAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Guid?>(null);
    }
}