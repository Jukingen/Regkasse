namespace KasseAPI_Final.DTOs;

public sealed class DepExportStatisticsDto
{
    public int TotalExports { get; set; }

    public int SuccessfulExports { get; set; }

    public int FailedExports { get; set; }

    /// <summary>0–100 success percentage among terminal statuses (Completed + Failed).</summary>
    public double SuccessRate { get; set; }

    public Dictionary<string, int> ExportsByType { get; set; } = new();

    public Dictionary<string, int> ExportsByYear { get; set; } = new();

    /// <summary>Average completed export size in bytes.</summary>
    public double AverageExportSizeBytes { get; set; }

    /// <summary>Total storage of completed exports in the window (megabytes).</summary>
    public double TotalStorageUsedMb { get; set; }

    public DateTime? LastExportDate { get; set; }

    public DateTime? NextDueDate { get; set; }

    public DateTime FromUtc { get; set; }

    public DateTime ToUtc { get; set; }
}

public sealed class DepExportTrendPointDto
{
    /// <summary>Month start UTC (yyyy-MM-01).</summary>
    public DateTime PeriodStartUtc { get; set; }

    public string Label { get; set; } = string.Empty;

    public int TotalExports { get; set; }

    public int SuccessfulExports { get; set; }

    public int FailedExports { get; set; }

    public long TotalSizeBytes { get; set; }
}

public sealed class DepExportForecastDto
{
    public DateTime GeneratedAtUtc { get; set; }

    public DateTime? NextDueDate { get; set; }

    public string? NextRequirementTitle { get; set; }

    /// <summary>Average successful exports per month over the lookback window.</summary>
    public double AverageMonthlyExports { get; set; }

    /// <summary>Simple projected successful export count for upcoming months.</summary>
    public IReadOnlyList<DepExportForecastPointDto> Points { get; set; } =
        Array.Empty<DepExportForecastPointDto>();

    public string Method { get; set; } =
        "Linear average of successful exports over the last 12 months (operational estimate).";
}

public sealed class DepExportForecastPointDto
{
    public DateTime PeriodStartUtc { get; set; }

    public string Label { get; set; } = string.Empty;

    public double ProjectedExports { get; set; }

    public bool HasKnownDueDate { get; set; }
}
