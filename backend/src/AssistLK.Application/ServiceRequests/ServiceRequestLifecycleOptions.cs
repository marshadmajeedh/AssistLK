namespace AssistLK.Application.ServiceRequests;

/// <summary>
/// Strongly-typed configuration options for C1 ServiceRequest lifecycle matching windows and expiration.
/// </summary>
public sealed class ServiceRequestLifecycleOptions
{
    public const string SectionName = "ServiceRequestLifecycle";

    /// <summary>
    /// Stale timeout in hours for an AwaitingInformation request with unanswered clarification. Defaults to 24 hours.
    /// </summary>
    public int AwaitingInformationTimeoutHours { get; set; } = 24;

    /// <summary>
    /// Active matching window in hours for ReadyForMatching requests. Defaults to 24 hours.
    /// </summary>
    public int MatchingWindowHours { get; set; } = 24;

    /// <summary>
    /// Initial delay in seconds before the background lifecycle expiration worker begins polling. Defaults to 60 seconds.
    /// </summary>
    public int InitialDelaySeconds { get; set; } = 60;

    /// <summary>
    /// Polling interval in minutes between background lifecycle expiration passes. Defaults to 15 minutes.
    /// </summary>
    public int PollIntervalMinutes { get; set; } = 15;

    /// <summary>
    /// Whether background lifecycle expiration is enabled. Defaults to true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Validates the configuration options according to C1 lifecycle safety rules.
    /// </summary>
    public void Validate()
    {
        if (AwaitingInformationTimeoutHours <= 0)
        {
            throw new InvalidOperationException(
                "ServiceRequestLifecycle:AwaitingInformationTimeoutHours must be greater than 0.");
        }

        if (MatchingWindowHours <= 0)
        {
            throw new InvalidOperationException(
                "ServiceRequestLifecycle:MatchingWindowHours must be greater than 0.");
        }

        if (InitialDelaySeconds < 0)
        {
            throw new InvalidOperationException(
                "ServiceRequestLifecycle:InitialDelaySeconds must not be negative.");
        }

        if (PollIntervalMinutes <= 0)
        {
            throw new InvalidOperationException(
                "ServiceRequestLifecycle:PollIntervalMinutes must be greater than 0.");
        }
    }
}
