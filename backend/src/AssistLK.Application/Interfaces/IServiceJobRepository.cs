namespace AssistLK.Application.Interfaces;

public interface IServiceJobRepository
{
    Task<Guid?> GetIdByServiceRequestIdAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default);
}