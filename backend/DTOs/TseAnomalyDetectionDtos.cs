namespace KasseAPI_Final.DTOs;

public sealed class TseAnomalyDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? DeviceId { get; set; }
    public string? DeviceLabel { get; set; }
    public string MetricName { get; set; } = string.Empty;
    public double CurrentValue { get; set; }
    public double ExpectedValue { get; set; }
    public double Deviation { get; set; }
    public string Severity { get; set; } = "Info";
    public string Description { get; set; } = string.Empty;
    public string? SuggestedAction { get; set; }
    public DateTime DetectedAt { get; set; }
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public sealed class TseAnomalyResultDto
{
    public Guid TenantId { get; set; }
    public Guid? DeviceId { get; set; }
    public DateTime DetectedAt { get; set; }
    public IReadOnlyList<TseAnomalyDto> Anomalies { get; set; } = Array.Empty<TseAnomalyDto>();
    public string OverallSeverity { get; set; } = "Info";
    public bool RequiresAction { get; set; }
    public string Summary { get; set; } = string.Empty;
    /// <summary>Always true — statistical baseline detector, not a certified ML model.</summary>
    public bool DiagnosticOnly { get; set; } = true;
}

public sealed class TseAnomalyReportDto
{
    public Guid TenantId { get; set; }
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public int TotalAnomalies { get; set; }
    public int OpenAnomalies { get; set; }
    public int CriticalCount { get; set; }
    public int HighCount { get; set; }
    public int MediumCount { get; set; }
    public int LowCount { get; set; }
    public int InfoCount { get; set; }
    public string OverallSeverity { get; set; } = "Info";
    public IReadOnlyList<TseAnomalyDto> Anomalies { get; set; } = Array.Empty<TseAnomalyDto>();
    public bool DiagnosticOnly { get; set; } = true;
}

public sealed class TseAnomalyDashboardDto
{
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public int CriticalCount { get; set; }
    public int HighCount { get; set; }
    public int MediumCount { get; set; }
    public int LowCount { get; set; }
    public int InfoCount { get; set; }
    public int OpenCount { get; set; }
    public TseAnomalyResultDto? LastDetection { get; set; }
    public IReadOnlyList<TseAnomalyDto> Anomalies { get; set; } = Array.Empty<TseAnomalyDto>();
}

public sealed class TseAnomalyCheckRequestDto
{
    public string MetricName { get; set; } = string.Empty;
    public double Value { get; set; }
}
