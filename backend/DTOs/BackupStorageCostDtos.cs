namespace KasseAPI_Final.DTOs;

/// <summary>
/// Indicative backup storage cost rollup (ops estimate — not a billing invoice).
/// </summary>
public sealed class BackupStorageCostResponseDto
{
    /// <summary>Succeeded logical-dump bytes summed, in GiB.</summary>
    public double TotalStorageGb { get; init; }

    /// <summary>Configured enqueue budget (<see cref="Services.Backup.BackupService.MaxStorageBytes"/>) in GiB.</summary>
    public double BudgetGb { get; init; }

    /// <summary>0–100 of budget consumed.</summary>
    public double UsagePercentage { get; init; }

    /// <summary>Estimated monthly EUR at configured Hot/Warm/Cold rates.</summary>
    public double MonthlyCostEur { get; init; }

    /// <summary>Blended EUR per GiB-month.</summary>
    public double CostPerGbEur { get; init; }

    public int BackupCount { get; init; }

    /// <summary>Average logical-dump size in MiB.</summary>
    public double AverageSizeMb { get; init; }

    /// <summary>Percent saved vs pricing all bytes as Hot.</summary>
    public double RetentionSavingsPercent { get; init; }

    /// <summary>Simple forward estimate (usage pressure may inflate).</summary>
    public double ProjectedMonthlyEur { get; init; }

    public bool SmartRetentionEnabled { get; init; }

    public bool StorageTierManagementEnabled { get; init; }

    public string Disclaimer { get; init; } =
        "Indicative storage cost model for operators. Not an invoice or cloud provider bill.";

    public List<BackupStorageTierCostRowDto> Tiers { get; init; } = new();

    public List<BackupStorageCostRecommendationDto> Recommendations { get; init; } = new();
}

public sealed class BackupStorageTierCostRowDto
{
    public string Name { get; init; } = string.Empty;
    public double SizeGb { get; init; }
    public double CostEur { get; init; }
    public string Access { get; init; } = string.Empty;
    public string Retention { get; init; } = string.Empty;
    public int ArtifactCount { get; init; }
}

public sealed class BackupStorageCostRecommendationDto
{
    public string Code { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public double SavingsPercent { get; init; }
}
