namespace AssistLK.Domain.Enums;

public enum ServiceRequestStatus
{
    Created,
    Analyzing,
    AwaitingInformation,
    Analyzed,
    ReadyForMatching,
    Cancelled
}