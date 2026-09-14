using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;

namespace AssistLK.Application.Interfaces;

public interface IServiceRequestRepository
{
    // Recovery must use current persisted revision/status, not an EF tracked snapshot.
    Task<ServiceRequest?> ReloadForRecoveryAsync(Guid serviceRequestId, CancellationToken cancellationToken = default)
        => GetByIdAsync(serviceRequestId, cancellationToken: cancellationToken);

    Task<ServiceRequest?> GetByIdAsync(
        Guid serviceRequestId,
        bool includeProblemAnalyses = false,
        CancellationToken cancellationToken = default);

    Task<ServiceRequest?> GetByIdAsync(
        Guid serviceRequestId,
        bool includeProblemAnalyses,
        bool includeClarifications,
        CancellationToken cancellationToken = default)
        => GetByIdAsync(serviceRequestId, includeProblemAnalyses, cancellationToken);

    Task<ServiceRequest?> GetByIdAndCustomerIdAsync(
        Guid serviceRequestId,
        Guid customerId,
        bool includeProblemAnalyses = false,
        CancellationToken cancellationToken = default);

    Task<ServiceRequest?> GetByIdAndCustomerIdAsync(
        Guid serviceRequestId,
        Guid customerId,
        bool includeProblemAnalyses,
        bool includeClarifications,
        CancellationToken cancellationToken = default)
        => GetByIdAndCustomerIdAsync(serviceRequestId, customerId, includeProblemAnalyses, cancellationToken);

    Task<IReadOnlyList<ServiceRequest>> GetByCustomerIdAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ServiceRequest>> GetByStatusAsync(
        ServiceRequestStatus status,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ServiceRequest>> GetAllForAdminAsync(
        ServiceRequestStatus? status = null,
        string? category = null,
        ServiceRequestUrgency? urgency = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(ServiceRequest serviceRequest, CancellationToken cancellationToken = default);
    Task AddClarificationsAsync(
        IEnumerable<ServiceRequestClarification> clarifications,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
    void Update(ServiceRequest serviceRequest);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
