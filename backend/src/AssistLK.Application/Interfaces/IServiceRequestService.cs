using AssistLK.Application.ServiceRequests.DTOs;
using AssistLK.Domain.Enums;

namespace AssistLK.Application.Interfaces;

public interface IServiceRequestService
{
    Task<ServiceRequestResponse> CreateAsync(
        Guid customerId,
        CreateServiceRequestRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceRequestResponse> GetByIdAsync(
        Guid serviceRequestId,
        Guid customerId,
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

    Task<ServiceRequestStatus> GetPreAnalysisStatusAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default);

    Task<ServiceRequestResponse> RecoverFailedAnalysisAsync(
        Guid serviceRequestId,
        ServiceRequestStatus previousStatus,
        CancellationToken cancellationToken = default);

    Task<ServiceRequestResponse> MarkReadyForMatchingAsync(
        Guid serviceRequestId,
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ServiceRequestClarificationDto>> SubmitClarificationAnswersAsync(
        Guid customerId,
        Guid serviceRequestId,
        SubmitClarificationAnswersRequest request,
        CancellationToken cancellationToken = default);
}

