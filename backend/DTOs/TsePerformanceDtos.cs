namespace KasseAPI_Final.DTOs;

/// <summary>Aggregated TSE health-probe performance for a device over a time window.</summary>
public sealed class TsePerformanceMetricsDto
{
    public Guid DeviceId { get; set; }
    public string? DeviceLabel { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public double AverageResponseTime { get; set; }
    public double MinResponseTime { get; set; }
    public double MaxResponseTime { get; set; }
    public int TimedSamples { get; set; }
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public int FailedRequests { get; set; }
    public double SuccessRate { get; set; }
    public double ErrorRate { get; set; }
    public int SlowThresholdMs { get; set; }
    public int CriticalThresholdMs { get; set; }
    public IReadOnlyList<TsePerformancePointDto> PerformanceHistory { get; set; } =
        Array.Empty<TsePerformancePointDto>();
}

public sealed class TsePerformancePointDto
{
    public DateTime Timestamp { get; set; }
    public double? ResponseTimeMs { get; set; }
    public bool Success { get; set; }
    public int HealthScore { get; set; }
    public string HealthStatus { get; set; } = "Healthy";
}

public sealed class TsePerformanceAlertDto
{
    public Guid DeviceId { get; set; }
    public Guid? TenantId { get; set; }
    public bool HasAnomaly { get; set; }
    public string Severity { get; set; } = "Info";
    public IReadOnlyList<string> Codes { get; set; } = Array.Empty<string>();
    public string Message { get; set; } = string.Empty;
    public bool AlertPublished { get; set; }
    public TsePerformanceMetricsDto? Metrics { get; set; }
}
