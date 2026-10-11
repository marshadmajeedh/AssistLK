namespace AssistLK.Application.Auth;

/// <summary>
/// Configuration options for background registration challenge retention cleanup.
/// </summary>
public sealed class RegistrationChallengeRetentionOptions
{
    public const string SectionName = "RegistrationChallengeRetention";

    /// <summary>
    /// Whether background challenge cleanup is enabled. Defaults to true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Initial delay in seconds before the first cleanup cycle runs after host startup.
    /// Default: 60 seconds.
    /// </summary>
    public int InitialDelaySeconds { get; set; } = 60;

    /// <summary>
    /// Interval in hours between scheduled cleanup cycles.
    /// Default: 24 hours.
    /// </summary>
    public int IntervalHours { get; set; } = 24;

    /// <summary>
    /// Optional interval in seconds, useful for testing. Overrides IntervalHours when set to > 0.
    /// </summary>
    public int? IntervalSeconds { get; set; }

    /// <summary>
    /// Retention window in hours. Challenges expired or consumed older than this threshold are deleted.
    /// Default: 24 hours.
    /// </summary>
    public int RetentionHours { get; set; } = 24;

    /// <summary>
    /// Optional retention duration in seconds, useful for testing. Overrides RetentionHours when set to > 0.
    /// </summary>
    public int? RetentionSeconds { get; set; }

    /// <summary>
    /// Calculates the effective retention time span.
    /// </summary>
    public TimeSpan GetRetentionTimeSpan()
    {
        if (RetentionSeconds.HasValue && RetentionSeconds.Value > 0)
        {
            return TimeSpan.FromSeconds(RetentionSeconds.Value);
        }

        return TimeSpan.FromHours(RetentionHours);
    }

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public void Validate()
    {
        if (InitialDelaySeconds < 0)
        {
            throw new InvalidOperationException("RegistrationChallengeRetention:InitialDelaySeconds must not be negative.");
        }

        if (IntervalHours < 1 && (!IntervalSeconds.HasValue || IntervalSeconds.Value < 1))
        {
            throw new InvalidOperationException("RegistrationChallengeRetention:IntervalHours must be at least 1 hour.");
        }

        if (RetentionHours < 1 && (!RetentionSeconds.HasValue || RetentionSeconds.Value < 1))
        {
            throw new InvalidOperationException("RegistrationChallengeRetention:RetentionHours must be at least 1 hour.");
        }
    }
}
