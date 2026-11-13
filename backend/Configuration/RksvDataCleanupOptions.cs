namespace KasseAPI_Final.Configuration;

/// <summary>
/// Cold-archive of fiscal/RKSV rows past the legal retention window.
/// Live signature-chain rows are never deleted by default (<see cref="HardDeleteEnabled"/> stays false).
/// </summary>
public sealed class RksvDataCleanupOptions
{
    public const string SectionName = "RksvDataCleanup";

    /// <summary>When false (default), the hosted service only idles.</summary>
    public bool Enabled { get; set; }

    /// <summary>Austrian RKSV minimum retention before cold-archive eligibility.</summary>
    public int RetentionYears { get; set; } = 7;

    /// <summary>
    /// Extra years to keep cold archives on disk after the retention cutoff (safety buffer).
    /// Live DB fiscal rows are still not deleted unless <see cref="HardDeleteEnabled"/> is true
    /// (hard delete remains refused for signature-chain integrity).
    /// </summary>
    public int ExtraArchiveYears { get; set; } = 3;

    /// <summary>
    /// When true, the worker may attempt hard-delete of archived live rows.
    /// Default false — hard delete of fiscal payments is refused (TSE/RKSV chain integrity).
    /// </summary>
    public bool HardDeleteEnabled { get; set; }

    /// <summary>Relative to content root unless rooted. Default <c>App_Data/rksv-cold-archives</c>.</summary>
    public string ArchiveRootRelativeDirectory { get; set; } = "App_Data/rksv-cold-archives";

    /// <summary>Max payment rows archived per daily sweep.</summary>
    public int MaxBatchSize { get; set; } = 500;

    /// <summary>Delay between sweeps.</summary>
    public int IntervalHours { get; set; } = 24;

    /// <summary>Startup grace before first sweep (minutes).</summary>
    public int StartupGraceMinutes { get; set; } = 5;
}
