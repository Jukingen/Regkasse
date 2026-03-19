namespace KasseAPI_Final.Configuration;

/// <summary>
/// Offline replay behaviour. Legacy structural fallback can be disabled after payload_hash repair.
/// </summary>
public class OfflineReplayOptions
{
    public const string SectionName = "OfflineReplay";

    /// <summary>
    /// When true, last-resort structural JSON match is used when hash paths (direct + recomputed) did not match.
    /// Set to false after legacy payload_hash backfill is complete and no rows need structural fallback.
    /// </summary>
    public bool AllowStructuralFallback { get; set; } = true;

    /// <summary>
    /// Max recent rows per register to scan in structural fallback (only when AllowStructuralFallback is true).
    /// Smaller window reduces wrong-match risk and cost. Default 50.
    /// </summary>
    public int StructuralPayloadFallbackLimit { get; set; } = 50;

    /// <summary>
    /// Max wait time in ms for advisory lock acquire (try-lock + retry). Prevents deadlock-like long waits. Default 10000 (10s).
    /// </summary>
    public int MaxLockWaitMs { get; set; } = 10_000;

    /// <summary>
    /// Interval in ms between try-lock attempts. Default 100.
    /// </summary>
    public int LockRetryIntervalMs { get; set; } = 100;
}
