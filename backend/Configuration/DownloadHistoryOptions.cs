namespace KasseAPI_Final.Configuration;

/// <summary>Retention for <c>download_history</c> rows.</summary>
public sealed class DownloadHistoryOptions
{
    public const string SectionName = "DownloadHistory";

    /// <summary>Delete rows older than this many days (default 30).</summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>How often the cleanup hosted service runs (minutes).</summary>
    public int CleanupIntervalMinutes { get; set; } = 1440;

    public bool CleanupEnabled { get; set; } = true;
}

