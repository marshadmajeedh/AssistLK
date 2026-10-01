using AssistLK.Agents.DTOs;

namespace AssistLK.Agents.Abstractions;

public interface IQuotationBookingAgentClient
{
    Task<QuotationWorkflowStartResponse> StartWorkflowAsync(
        QuotationWorkflowStartRequest request,
        CancellationToken cancellationToken = default);

    Task<QuotationWorkflowResumeResponse> ResumeWorkflowAsync(
        QuotationWorkflowResumeRequest request,
        CancellationToken cancellationToken = default);
}