namespace AssistLK.Domain.Enums;

public enum ServiceRequestStatus
{
    Created,
    Analyzing,
    AwaitingInformation,
    Analyzed,
    ReadyForMatching,
    Completed, // <--- මෙන්න මේක එකතු කරන්න
    Cancelled
}