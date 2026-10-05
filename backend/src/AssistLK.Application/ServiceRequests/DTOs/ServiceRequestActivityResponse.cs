using System;

namespace AssistLK.Application.ServiceRequests.DTOs;

public class ServiceRequestActivityResponse
{
    public Guid ServiceRequestId { get; set; }
    public Guid? ServiceJobId { get; set; }
    public string? ServiceJobStatus { get; set; }
    public CompletionRecordResponse? CompletionRecord { get; set; }
    public FeedbackSummaryResponse? Feedback { get; set; }
    public string? QuotationStatus { get; set; }
    public string? BookingStatus { get; set; }
    public ProviderActivityInfo? Provider { get; set; }
}

public class ProviderActivityInfo
{
    public Guid ProviderId { get; set; }
    public string? BusinessName { get; set; }
}
