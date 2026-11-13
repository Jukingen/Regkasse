namespace KasseAPI_Final.Configuration;

/// <summary>
/// Periodic re-hash of recent succeeded backup artifacts (integrity only; not restore drills).
/// </summary>
public sealed class BackupReVerificationOptions
{
    public const string SectionName = "BackupReVerification";

    /// <summary>When false, hosted re-verification does not run.</summary>
    public bool Enabled { get; set; }

    /// <summary>Delay between ticks. Minimum enforced: 1 hour.</summary>
    public int CheckIntervalHours { get; set; } = 24;

    /// <summary>Only re-verify succeeded runs completed within this many days.</summary>
    public int RetentionDays { get; set; } = 7;

    /// <summary>Max succeeded runs to re-verify per tick (CPU/IO guard).</summary>
    public int MaxRunsPerTick { get; set; } = 20;

    public TimeSpan GetCheckInterval()
    {
        var hours = CheckIntervalHours <= 0 ? 24 : CheckIntervalHours;
        var interval = TimeSpan.FromHours(hours);
        return interval < TimeSpan.FromHours(1) ? TimeSpan.FromHours(1) : interval;
    }
}
