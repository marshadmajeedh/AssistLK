using AssistLK.Application.ServiceRequests.DTOs;

namespace AssistLK.Application.Interfaces;

public interface IServiceRequestService
{
    Task<ServiceRequestResponse> CreateAsync(
        Guid customerId,
        CreateServiceRequestRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceRequestResponse> GetByIdAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ServiceRequestResponse>> GetCurrentCustomerRequestsAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<ServiceRequestResponse> UpdateAsync(
        Guid customerId,
        Guid serviceRequestId,
        UpdateServiceRequestRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceRequestResponse> CancelAsync(
        Guid customerId,
        Guid serviceRequestId,
        CancellationToken cancellationToken = default);

    Task<ProblemAnalysisResponse> ApplyProblemAnalysisResultAsync(
        ApplyProblemAnalysisResult result,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProblemAnalysisResponse>> GetProblemAnalysesAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default);

    Task<ServiceRequestForMatchingResponse?> GetReadyForMatchingAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default);

    Task<ServiceRequestResponse> BeginAnalysisAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default);

    Task<ServiceRequestResponse> MarkReadyForMatchingAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default);
}
