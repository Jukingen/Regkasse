namespace KasseAPI_Final.DTOs;

public sealed class TseBiDailyTrendDto
{
    public DateTime Date { get; set; }
    public double Value { get; set; }
    public string Label { get; set; } = string.Empty;
}

public sealed class TseBiNamedCountDto
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class TseBiDashboardDto
{
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public DateTime GeneratedAt { get; set; }
    public int LookbackDays { get; set; }

    public int TotalTransactions { get; set; }
    public int ActiveDevices { get; set; }
    public int TotalDevices { get; set; }
    public double OverallHealthScore { get; set; }

    public IReadOnlyList<TseBiDailyTrendDto> TransactionTrends { get; set; } =
        Array.Empty<TseBiDailyTrendDto>();
    public IReadOnlyList<TseBiDailyTrendDto> HealthTrends { get; set; } =
        Array.Empty<TseBiDailyTrendDto>();

    public IReadOnlyList<TseBiNamedCountDto> StatusBreakdown { get; set; } =
        Array.Empty<TseBiNamedCountDto>();
    public IReadOnlyList<TseBiNamedCountDto> ProviderBreakdown { get; set; } =
        Array.Empty<TseBiNamedCountDto>();

    public int CriticalAlerts { get; set; }
    public int WarningAlerts { get; set; }
    public int InfoAlerts { get; set; }

    /// <summary>Diagnostic BI only — not a Finanzamt / DEP export.</summary>
    public bool DiagnosticOnly { get; set; } = true;
}

public sealed class TseBiReportRequestDto
{
    public Guid TenantId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int LookbackDays { get; set; } = 30;
}

public sealed class TseBiReportDto
{
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public DateTime GeneratedAt { get; set; }
    public TseBiDashboardDto Dashboard { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
    public bool DiagnosticOnly { get; set; } = true;
}

public sealed class TseBiExportRequestDto
{
    public Guid TenantId { get; set; }
    /// <summary>csv | pdf</summary>
    public string Format { get; set; } = "csv";
    public int LookbackDays { get; set; } = 30;
}

public sealed class TseBiExportResultDto
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "text/csv";
    public string ContentBase64 { get; set; } = string.Empty;
    public long ByteLength { get; set; }
    public bool DiagnosticOnly { get; set; } = true;
}
