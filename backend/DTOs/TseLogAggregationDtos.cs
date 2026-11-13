namespace KasseAPI_Final.DTOs;

public sealed class TseLogEntryDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public DateTime Timestamp { get; set; }
    /// <summary>Error | Warning | Info</summary>
    public string Level { get; set; } = "Info";
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? DeviceId { get; set; }
    public string? DeviceLabel { get; set; }
    public string? Provider { get; set; }
    public string? Category { get; set; }
    public IReadOnlyDictionary<string, string>? Metadata { get; set; }
}

public sealed class TseLogPatternDto
{
    public string Pattern { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Level { get; set; } = "Info";
    public string? SampleMessage { get; set; }
}

public sealed class TseLogAnomalyDto
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning";
    public string Message { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
    public double Score { get; set; }
}

public sealed class TseLogAggregationResultDto
{
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int TotalLogs { get; set; }
    public int ErrorLogs { get; set; }
    public int WarningLogs { get; set; }
    public int InfoLogs { get; set; }
    public IReadOnlyDictionary<string, int> LogsByProvider { get; set; } =
        new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> LogsByDevice { get; set; } =
        new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> LogsBySource { get; set; } =
        new Dictionary<string, int>();
    public IReadOnlyList<TseLogPatternDto> Patterns { get; set; } = Array.Empty<TseLogPatternDto>();
    public IReadOnlyList<TseLogAnomalyDto> Anomalies { get; set; } = Array.Empty<TseLogAnomalyDto>();
    public IReadOnlyList<TseLogEntryDto> RecentLogs { get; set; } = Array.Empty<TseLogEntryDto>();
}

public sealed class TseLogSearchRequestDto
{
    public Guid TenantId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string? Query { get; set; }
    public string? Level { get; set; }
    public string? Provider { get; set; }
    public string? Source { get; set; }
    public Guid? DeviceId { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; } = 100;
}

public sealed class TseLogSearchResultDto
{
    public Guid TenantId { get; set; }
    public int TotalMatched { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
    public IReadOnlyList<TseLogEntryDto> Logs { get; set; } = Array.Empty<TseLogEntryDto>();
}

public sealed class TseLogAnalysisRequestDto
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    /// <summary>Optional focus: Error | Warning | All</summary>
    public string FocusLevel { get; set; } = "All";
}

public sealed class TseLogAnalysisReportDto
{
    public Guid TenantId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime GeneratedAt { get; set; }
    public string Summary { get; set; } = string.Empty;
    public double ErrorRatePercent { get; set; }
    public double WarningRatePercent { get; set; }
    public IReadOnlyList<TseLogPatternDto> TopPatterns { get; set; } = Array.Empty<TseLogPatternDto>();
    public IReadOnlyList<TseLogAnomalyDto> Anomalies { get; set; } = Array.Empty<TseLogAnomalyDto>();
    public IReadOnlyList<string> Recommendations { get; set; } = Array.Empty<string>();
    public TseLogAggregationResultDto Aggregation { get; set; } = new();
}

public static class TseLogLevels
{
    public const string Error = "Error";
    public const string Warning = "Warning";
    public const string Info = "Info";
}

public static class TseLogSources
{
    public const string Activity = "Activity";
    public const string Failover = "Failover";
    public const string Incident = "Incident";
    public const string HealthSample = "HealthSample";
}
