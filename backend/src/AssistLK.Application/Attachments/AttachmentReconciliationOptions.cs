namespace AssistLK.Application.Attachments;

/// <summary>
/// Configuration options for background attachment reconciliation maintenance worker.
/// </summary>
public sealed class AttachmentReconciliationOptions
{
    public const string SectionName = "AttachmentReconciliation";

    /// <summary>
    /// Whether background attachment reconciliation is enabled. Defaults to true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Initial delay in seconds before the first reconciliation cycle runs after host startup.
    /// Maintenance default: 60 seconds (conservative threshold: 30-60s).
    /// </summary>
    public int InitialDelaySeconds { get; set; } = 60;

    /// <summary>
    /// Interval in hours between scheduled reconciliation cycles.
    /// Maintenance default: 24 hours (conservative period: 12-24h).
    /// </summary>
    public int IntervalHours { get; set; } = 24;

    /// <summary>
    /// Optional interval in seconds, useful for testing. Overrides IntervalHours when set to > 0.
    /// </summary>
    public int? IntervalSeconds { get; set; }

    /// <summary>
    /// Minimum age of physical files in minutes before they are considered eligible for cleanup.
    /// Grace period prevents in-flight, actively-uploading files from being considered orphans.
    /// Default: 1440 minutes (24 hours / 1 day), preserving the existing 1-day safety window.
    /// </summary>
    public int MinimumFileAgeMinutes { get; set; } = 1440;

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public void Validate()
    {
        if (InitialDelaySeconds < 0)
        {
            throw new InvalidOperationException("AttachmentReconciliation:InitialDelaySeconds must not be negative.");
        }

        if (IntervalHours < 1 && (!IntervalSeconds.HasValue || IntervalSeconds.Value < 1))
        {
            throw new InvalidOperationException("AttachmentReconciliation:IntervalHours must be at least 1 hour.");
        }

        if (MinimumFileAgeMinutes < 1)
        {
            throw new InvalidOperationException("AttachmentReconciliation:MinimumFileAgeMinutes must be at least 1 minute.");
        }
    }
}
