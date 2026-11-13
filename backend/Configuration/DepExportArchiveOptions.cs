namespace KasseAPI_Final.Configuration;

/// <summary>
/// Long-term disk archive for completed DEP §7 export JSON (default 7-year RKSV retention).
/// Operates on <c>dep_export_history</c> rows — not cron <c>dep_export_schedules</c>.
/// </summary>
public sealed class DepExportArchiveOptions
{
    public const string SectionName = "DepExportArchive";

    /// <summary>When false, archive/purge APIs and workers no-op (except explicit manual archive if forced).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Relative to content root unless rooted. Default <c>App_Data/dep-export-archives</c>.</summary>
    public string ArchiveRootRelativeDirectory { get; set; } = "App_Data/dep-export-archives";

    /// <summary>Legal retention window before purge eligibility (Austrian RKSV minimum).</summary>
    public int RetentionYears { get; set; } = 7;

    /// <summary>When true, <see cref="Services.DepExportHistoryService"/> archives after each completed export.</summary>
    public bool AutoArchiveOnComplete { get; set; } = true;

    /// <summary>When true, hosted worker purges expired archive files.</summary>
    public bool PurgeEnabled { get; set; } = true;

    /// <summary>Hosted service delay between archive/purge sweeps (hours). Minimum 1.</summary>
    public int CheckIntervalHours { get; set; } = 24;

    /// <summary>Max history rows archived per hosted sweep.</summary>
    public int MaxBatchSize { get; set; } = 100;
}
