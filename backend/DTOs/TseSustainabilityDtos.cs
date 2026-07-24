namespace KasseAPI_Final.DTOs;

/// <summary>Indicative TSE green-IT sustainability report (not certified LCA / ESG audit).</summary>
public sealed class TseSustainabilityReportDto
{
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime GeneratedAt { get; set; }

    // Carbon footprint
    public double TotalCarbonEmission { get; set; }
    public double PerTransactionEmission { get; set; }
    public double PerDeviceEmission { get; set; }

    // Energy usage
    public double TotalEnergyUsage { get; set; }
    public double AverageDeviceEnergyUsage { get; set; }

    // Savings vs cloud-only baseline
    public double CarbonSaved { get; set; }
    public double EnergySaved { get; set; }
    public double CostSaved { get; set; }

    // Comparison
    public double IndustryAverage { get; set; }
    public double Percentile { get; set; }

    public int ActiveDeviceCount { get; set; }
    public int SoftOrDemoDeviceCount { get; set; }
    public int SignedTransactions { get; set; }
    public int TotalTransactions { get; set; }

    public IReadOnlyList<TseSustainabilityTrendDto> CarbonTrend { get; set; } =
        Array.Empty<TseSustainabilityTrendDto>();

    public bool DiagnosticOnly { get; set; } = true;
}

public sealed class TseSustainabilityTrendDto
{
    public DateTime Date { get; set; }
    public string Label { get; set; } = string.Empty;
    public double CarbonKg { get; set; }
    public double EnergyKwh { get; set; }
    public int TransactionCount { get; set; }
}

public sealed class TseCarbonFootprintDto
{
    public Guid TenantId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public double TotalKgCo2 { get; set; }
    public double DeviceEnergyKgCo2 { get; set; }
    public double TransactionApiKgCo2 { get; set; }
    public double PerTransactionKgCo2 { get; set; }
    public double IndustryAverageKgCo2PerTransaction { get; set; }
    public int SignedTransactions { get; set; }
    public int ActiveDeviceCount { get; set; }
    public bool DiagnosticOnly { get; set; } = true;
}

public sealed class TseSustainabilityOptimizationResultDto
{
    public Guid TenantId { get; set; }
    public DateTime GeneratedAt { get; set; }
    public double PotentialCarbonSavedKg { get; set; }
    public double PotentialEnergySavedKwh { get; set; }
    public double PotentialCostSavedEur { get; set; }
    public IReadOnlyList<TseSustainabilitySuggestionDto> Suggestions { get; set; } =
        Array.Empty<TseSustainabilitySuggestionDto>();
    public bool DiagnosticOnly { get; set; } = true;
}

public sealed class TseSustainabilitySuggestionDto
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = "Info";
    public double EstimatedCarbonSavedKgPerMonth { get; set; }
    public double EstimatedEnergySavedKwhPerMonth { get; set; }
    public double EstimatedCostSavedEurPerMonth { get; set; }
}
