namespace KasseAPI_Final.Services;

public sealed class DownloadHistoryKindStatDto
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public int Count { get; init; }
    public double Percent { get; init; }
}

public sealed class DownloadHistoryUserStatDto
{
    public string UserId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public int Count { get; init; }
}

public sealed class DownloadHistoryTenantStatDto
{
    public Guid TenantId { get; init; }
    public string TenantSlug { get; init; } = string.Empty;
    public string TenantName { get; init; } = string.Empty;
    public int Count { get; init; }
    public double Percent { get; init; }
}

public sealed class DownloadHistoryTrendPointDto
{
    public string PeriodKey { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public int Count { get; init; }
    public long TotalBytes { get; init; }
}

public sealed class DownloadHistorySlowExportDto
{
    public Guid Id { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string? SourceKind { get; init; }
    public string FileType { get; init; } = string.Empty;
    public long? FileSize { get; init; }
    public int? DurationMs { get; init; }
    public DateTime DownloadedAt { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    /// <summary><c>duration</c> when DurationMs known; otherwise <c>size</c> proxy.</summary>
    public string RankBy { get; init; } = "size";
}

public sealed class DownloadHistoryAnalyticsDto
{
    public int TotalCount { get; init; }
    public int TodayCount { get; init; }
    public int MonthCount { get; init; }
    public long TotalBytes { get; init; }
    public int RetentionDays { get; init; }
    public bool IncludesPlatformTenants { get; init; }
    public IReadOnlyList<DownloadHistoryKindStatDto> TopKinds { get; init; } = [];
    public IReadOnlyList<DownloadHistoryUserStatDto> TopUsers { get; init; } = [];
    public IReadOnlyList<DownloadHistoryTenantStatDto> TopTenants { get; init; } = [];
    public IReadOnlyList<DownloadHistoryTrendPointDto> DailyTrend { get; init; } = [];
    public IReadOnlyList<DownloadHistoryTrendPointDto> WeeklyTrend { get; init; } = [];
    public IReadOnlyList<DownloadHistoryTrendPointDto> MonthlyTrend { get; init; } = [];
    public IReadOnlyList<DownloadHistorySlowExportDto> SlowExports { get; init; } = [];
}
