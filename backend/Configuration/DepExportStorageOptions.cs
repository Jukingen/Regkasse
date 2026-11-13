namespace KasseAPI_Final.Configuration;

/// <summary>
/// On-disk storage for completed DEP §7 export JSON (manual + scheduled).
/// Distinct from long-term <see cref="DepExportArchiveOptions"/> retention tree.
/// </summary>
public sealed class DepExportStorageOptions
{
    public const string SectionName = "DepExportStorage";

    /// <summary>
    /// Relative to content root unless rooted.
    /// Default <c>App_Data/dep-exports</c>. Absolute paths (e.g. <c>C:\data\dep-exports</c>) are supported.
    /// </summary>
    public string StorageRootRelativeDirectory { get; set; } = "App_Data/dep-exports";

    /// <summary>Lifetime for opaque download tokens issued via the download-token API.</summary>
    public int DownloadTokenTtlHours { get; set; } = 24;

    /// <summary>When true, a download token is issued automatically on each completed export.</summary>
    public bool IssueDownloadTokenOnComplete { get; set; } = true;

    /// <summary>
    /// Hot (working) storage retention before cleanup of <c>App_Data/dep-exports</c> copies.
    /// Archived copies under <see cref="DepExportArchiveOptions"/> remain for the legal window.
    /// </summary>
    public int HotStorageRetentionDays { get; set; } = 7;

    /// <summary>When true, hosted sweep deletes expired hot storage files and clears expired download tokens.</summary>
    public bool CleanupEnabled { get; set; } = true;

    /// <summary>Hosted cleanup delay between sweeps (hours). Minimum 1. Default 24.</summary>
    public int CleanupIntervalHours { get; set; } = 24;

    /// <summary>Max history rows processed per hot-storage cleanup sweep.</summary>
    public int CleanupMaxBatchSize { get; set; } = 200;

    /// <summary>
    /// Soft metadata retention for Failed / already-purged history rows (hard delete).
    /// Completed+archived fiscal rows are kept until the 7-year archive purge.
    /// </summary>
    public int MetadataRetentionDays { get; set; } = 30;
}
