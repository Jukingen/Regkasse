namespace KasseAPI_Final.DTOs;

/// <summary>Indicative TSE operating-cost report (not an invoice).</summary>
public sealed class TseCostReportDto
{
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public string? TenantSlug { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime GeneratedAt { get; set; }

    public int TotalTransactions { get; set; }
    public int SignedTransactions { get; set; }
    public int ActiveDeviceCount { get; set; }
    public int BackupDeviceCount { get; set; }

    public decimal TotalCost { get; set; }
    public decimal AverageCostPerTransaction { get; set; }
    public string Currency { get; set; } = "EUR";

    public IReadOnlyDictionary<string, decimal> CostBreakdown { get; set; } =
        new Dictionary<string, decimal>();

    public IReadOnlyList<TseCostTrendDto> DailyTrends { get; set; } = Array.Empty<TseCostTrendDto>();

    public bool HasCostAnomaly { get; set; }
    public string? AnomalyDescription { get; set; }

    public IReadOnlyList<TseCostSavingRecommendationDto> Recommendations { get; set; } =
        Array.Empty<TseCostSavingRecommendationDto>();

    public decimal PotentialSavings { get; set; }
}

public sealed class TseCostTrendDto
{
    public DateTime Date { get; set; }
    public int TransactionCount { get; set; }
    public decimal EstimatedCost { get; set; }
}

public sealed class TseCostSavingRecommendationDto
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = "Info";
    public decimal EstimatedMonthlySavings { get; set; }
}

public sealed class TseCostAlertDto
{
    public Guid TenantId { get; set; }
    public bool HasAnomaly { get; set; }
    public string Severity { get; set; } = "Info";
    public IReadOnlyList<string> Codes { get; set; } = Array.Empty<string>();
    public string Message { get; set; } = string.Empty;
    public bool AlertPublished { get; set; }
    public decimal CurrentPeriodCost { get; set; }
    public decimal BaselinePeriodCost { get; set; }
    public decimal CostDeltaPercent { get; set; }
    public TseCostReportDto? Report { get; set; }
}
