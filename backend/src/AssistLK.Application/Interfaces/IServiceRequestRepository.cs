using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;

namespace AssistLK.Application.Interfaces;

public interface IServiceRequestRepository
{
    Task<ServiceRequest?> GetByIdAsync(
        Guid serviceRequestId,
        bool includeProblemAnalyses = false,
        CancellationToken cancellationToken = default);

    Task<ServiceRequest?> GetByIdAndCustomerIdAsync(
        Guid serviceRequestId,
        Guid customerId,
        bool includeProblemAnalyses = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ServiceRequest>> GetByCustomerIdAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ServiceRequest>> GetByStatusAsync(
        ServiceRequestStatus status,
        CancellationToken cancellationToken = default);

    Task AddAsync(ServiceRequest serviceRequest, CancellationToken cancellationToken = default);
    void Update(ServiceRequest serviceRequest);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
