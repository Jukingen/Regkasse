namespace KasseAPI_Final.DTOs;

/// <summary>Period SLA report for a tenant's TSE fleet (operational; not a fiscal certificate).</summary>
public sealed class TseSlaReportDto
{
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public string? TenantSlug { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    public double UptimePercentage { get; set; }
    public double TargetUptimePercentage { get; set; }
    public bool IsUptimeTargetMet { get; set; }

    public double AverageResponseTime { get; set; }
    public double TargetResponseTime { get; set; }
    public bool IsResponseTimeTargetMet { get; set; }

    public int TotalTransactions { get; set; }
    public int SuccessfulTransactions { get; set; }
    public double SuccessRate { get; set; }
    public double TargetSuccessRate { get; set; }
    public bool IsSuccessRateTargetMet { get; set; }

    public int HealthSampleCount { get; set; }
    public int TimedSampleCount { get; set; }

    public IReadOnlyList<TseSlaViolationDto> Violations { get; set; } = Array.Empty<TseSlaViolationDto>();
    public string Grade { get; set; } = TseSlaGrades.N;
}

public sealed class TseSlaStatusDto
{
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public DateTime AsOfUtc { get; set; }
    public DateTime LookbackStartUtc { get; set; }
    public string Grade { get; set; } = TseSlaGrades.N;
    public bool IsCompliant { get; set; }
    public double UptimePercentage { get; set; }
    public double AverageResponseTime { get; set; }
    public double SuccessRate { get; set; }
    public int OpenViolationCount { get; set; }
    public TseSlaReportDto Report { get; set; } = new();
}

public sealed class TseSlaAlertDto
{
    public Guid TenantId { get; set; }
    public bool HasViolations { get; set; }
    public string Severity { get; set; } = "Info";
    public string Message { get; set; } = string.Empty;
    public bool AlertPublished { get; set; }
    public IReadOnlyList<TseSlaViolationDto> Violations { get; set; } = Array.Empty<TseSlaViolationDto>();
    public TseSlaReportDto? Report { get; set; }
}

public sealed class TseSlaViolationDto
{
    public string Code { get; set; } = string.Empty;
    public string Metric { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning";
    public string Message { get; set; } = string.Empty;
    public double ActualValue { get; set; }
    public double TargetValue { get; set; }
    public DateTime DetectedAt { get; set; }
}

public static class TseSlaGrades
{
    public const string A = "A";
    public const string B = "B";
    public const string C = "C";
    public const string D = "D";
    public const string F = "F";
    public const string N = "N";
}

public static class TseSlaViolationCodes
{
    public const string Uptime = "sla_uptime";
    public const string ResponseTime = "sla_response_time";
    public const string SuccessRate = "sla_success_rate";
}
