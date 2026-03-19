namespace KasseAPI_Final.Configuration;

/// <summary>
/// Options for the FinanzOnline automatic retry background job.
/// </summary>
public sealed class FinanzOnlineRetryJobOptions
{
    public const string SectionName = "FinanzOnlineRetryJob";

    /// <summary>Run interval (e.g. 2 minutes).</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Maximum automatic retry attempts per payment (e.g. 5). After this, status remains Pending and job stops picking it.</summary>
    public int MaxRetryCount { get; set; } = 5;

    /// <summary>Base delay in seconds for exponential backoff. Next attempt after BaseDelaySeconds * 2^RetryCount (capped at BackoffCapSeconds).</summary>
    public int BaseDelaySeconds { get; set; } = 60;

    /// <summary>Cap backoff at this many seconds (e.g. 1 hour).</summary>
    public int BackoffCapSeconds { get; set; } = 3600;

    /// <summary>Max number of payments to retry per run (batch size).</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>Alert when total Failed count in DB exceeds this threshold (log + optional sink).</summary>
    public int AlertFailedThreshold { get; set; } = 10;

    /// <summary>Alert when the same cash register has this many or more Failed/Pending-with-max-retries in recent window.</summary>
    public int RegisterRepeatedFailureThreshold { get; set; } = 3;

    /// <summary>When true, the background retry job is enabled. Set false to disable (e.g. for manual-only retry).</summary>
    public bool Enabled { get; set; } = true;
}
