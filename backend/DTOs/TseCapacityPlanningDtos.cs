namespace KasseAPI_Final.DTOs;

/// <summary>Tenant TSE signing-capacity snapshot (receipt volume vs configured device limits).</summary>
public sealed class TseCapacityReportDto
{
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public string? TenantSlug { get; set; }
    public DateTime GeneratedAt { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    /// <summary>Average receipts/day over the lookback window.</summary>
    public int DailyTransactionAverage { get; set; }

    /// <summary>Total receipts in the lookback window.</summary>
    public int MonthlyTransactionTotal { get; set; }

    /// <summary>Highest receipts in any single UTC hour in the lookback window.</summary>
    public int PeakHourlyTransactions { get; set; }

    public int ActiveSigningDevices { get; set; }
    public int MaxDailyCapacity { get; set; }
    public int MaxHourlyCapacity { get; set; }

    /// <summary>Daily average / max daily capacity × 100.</summary>
    public double CurrentUtilizationPercentage { get; set; }

    public int EstimatedNextMonthTransactions { get; set; }

    /// <summary>Projected date when daily volume would reach capacity; null when not approaching.</summary>
    public DateTime? EstimatedCapacityReachDate { get; set; }

    public bool IsNearCapacity { get; set; }
    public int LookbackDays { get; set; }
    public double DailyGrowthRatePercent { get; set; }

    /// <summary>Per-day receipt counts for trend charts.</summary>
    public IReadOnlyList<TseDailyTransactionTrendDto> DailyTrends { get; set; } =
        Array.Empty<TseDailyTransactionTrendDto>();

    public IReadOnlyList<string> Recommendations { get; set; } = Array.Empty<string>();
}

public sealed class TseDailyTransactionTrendDto
{
    public DateTime Date { get; set; }
    public int TransactionCount { get; set; }
    public int SignedCount { get; set; }
}

/// <summary>Forward projection of TSE signing volume for a tenant.</summary>
public sealed class TseForecastResultDto
{
    public Guid TenantId { get; set; }
    public int ForecastDays { get; set; }
    public DateTime GeneratedAt { get; set; }
    public int BaselineDailyAverage { get; set; }
    public int EstimatedTotalTransactions { get; set; }
    public int EstimatedDailyAverage { get; set; }
    public int EstimatedPeakHourly { get; set; }
    public double DailyGrowthRatePercent { get; set; }
    public string Confidence { get; set; } = "Low";
    public IReadOnlyList<TseForecastDayPointDto> DailyPoints { get; set; } = Array.Empty<TseForecastDayPointDto>();
}

public sealed class TseForecastDayPointDto
{
    public DateTime Date { get; set; }
    public int EstimatedTransactions { get; set; }
}

/// <summary>Capacity threshold evaluation + optional activity alert.</summary>
public sealed class TseCapacityAlertDto
{
    public Guid TenantId { get; set; }
    public bool HasAlert { get; set; }
    public bool IsNearCapacity { get; set; }
    public string Severity { get; set; } = "Info";
    public IReadOnlyList<string> Codes { get; set; } = Array.Empty<string>();
    public string Message { get; set; } = string.Empty;
    public double UtilizationPercentage { get; set; }
    public DateTime? EstimatedCapacityReachDate { get; set; }
    public bool AlertPublished { get; set; }
    public TseCapacityReportDto? Report { get; set; }
}
