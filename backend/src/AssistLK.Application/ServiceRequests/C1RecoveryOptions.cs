namespace AssistLK.Application.ServiceRequests;

/// <summary>
/// Configuration options for C1 stale analysis recovery.
/// </summary>
public sealed class C1RecoveryOptions
{
    public const string SectionName = "C1Recovery";

    /// <summary>
    /// Stale threshold in minutes for considering an Analyzing request crashed or orphaned. Defaults to 5 minutes.
    /// </summary>
    public int StaleAnalysisMinutes { get; set; } = 5;

    /// <summary>
    /// Periodic poll interval in seconds. Defaults to 60 seconds (1 minute).
    /// </summary>
    public int PollIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Initial delay in seconds before first background check runs. Defaults to 5 seconds.
    /// </summary>
    public int InitialDelaySeconds { get; set; } = 5;

    /// <summary>
    /// Whether background recovery is enabled. Defaults to true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public void Validate()
    {
        if (StaleAnalysisMinutes < 1)
        {
            throw new InvalidOperationException("C1Recovery:StaleAnalysisMinutes must be at least 1 minute.");
        }

        if (PollIntervalSeconds < 5)
        {
            throw new InvalidOperationException("C1Recovery:PollIntervalSeconds must be at least 5 seconds.");
        }

        if (InitialDelaySeconds < 0)
        {
            throw new InvalidOperationException("C1Recovery:InitialDelaySeconds must not be negative.");
        }
    }
}
