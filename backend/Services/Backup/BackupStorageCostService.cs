using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Builds an indicative Hot/Warm/Cold storage cost dashboard from succeeded logical dumps.
/// Rates are configurable ops estimates — not cloud billing.
/// </summary>
public sealed class BackupStorageCostService : IBackupStorageCostService
{
    public const double BytesPerGiB = 1024d * 1024d * 1024d;
    public const double BytesPerMiB = 1024d * 1024d;

    private readonly AppDbContext _db;
    private readonly IOptionsMonitor<BackupOptions> _options;

    public BackupStorageCostService(AppDbContext db, IOptionsMonitor<BackupOptions> options)
    {
        _db = db;
        _options = options;
    }

    public async Task<BackupStorageCostResponseDto> GetAsync(
        BackupRunAccessScope? accessScope = null,
        CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        var hotRate = opts.StorageCostHotEurPerGbMonth;
        var warmRate = opts.StorageCostWarmEurPerGbMonth;
        var coldRate = opts.StorageCostColdEurPerGbMonth;

        var runs = await AccessibleRuns(accessScope)
            .AsNoTracking()
            .Include(r => r.Artifacts)
            .Where(r => r.Status == BackupRunStatus.Succeeded)
            .ToListAsync(cancellationToken);

        var dumpRows = new List<(BackupStorageTier Tier, long Bytes)>();
        foreach (var run in runs)
        {
            var dump = run.Artifacts
                .Where(a => a.ArtifactType == BackupArtifactType.LogicalDump && a.ByteSize is > 0)
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefault();
            if (dump is null)
                continue;

            dumpRows.Add((dump.StorageTier, dump.ByteSize!.Value));
        }

        var usedBytes = dumpRows.Sum(r => r.Bytes);
        var budgetBytes = BackupService.MaxStorageBytes;
        var usagePct = budgetBytes <= 0
            ? 0d
            : Math.Round(Math.Min(100d, usedBytes / (double)budgetBytes * 100d), 1);

        var totalGb = Round3(usedBytes / BytesPerGiB);
        var budgetGb = Round3(budgetBytes / BytesPerGiB);
        var backupCount = dumpRows.Count;
        var averageMb = backupCount == 0
            ? 0d
            : Math.Round(usedBytes / (double)backupCount / BytesPerMiB, 1);

        var hotBytes = dumpRows.Where(r => r.Tier == BackupStorageTier.Hot).Sum(r => r.Bytes);
        var warmBytes = dumpRows.Where(r => r.Tier == BackupStorageTier.Warm).Sum(r => r.Bytes);
        var coldBytes = dumpRows.Where(r => r.Tier == BackupStorageTier.Cold).Sum(r => r.Bytes);

        var hotGb = hotBytes / BytesPerGiB;
        var warmGb = warmBytes / BytesPerGiB;
        var coldGb = coldBytes / BytesPerGiB;

        var hotCost = hotGb * (double)hotRate;
        var warmCost = warmGb * (double)warmRate;
        var coldCost = coldGb * (double)coldRate;
        var monthly = Round4(hotCost + warmCost + coldCost);
        var allHotCost = (usedBytes / BytesPerGiB) * (double)hotRate;
        var savings = allHotCost <= 0.0001
            ? 0d
            : Math.Round(Math.Max(0d, (allHotCost - monthly) / allHotCost * 100d), 1);
        var blended = totalGb <= 0.0001 ? (double)hotRate : Round4(monthly / totalGb);
        var projected = usagePct >= 80
            ? Round4(monthly * 1.15)
            : usagePct >= 50
                ? Round4(monthly * 1.05)
                : monthly;

        var tiers = new List<BackupStorageTierCostRowDto>
        {
            new()
            {
                Name = "Hot",
                SizeGb = Round3(hotGb),
                CostEur = Round4(hotCost),
                Access = "fast",
                Retention = $"≤{StorageTierService.HotRetentionDays}d",
                ArtifactCount = dumpRows.Count(r => r.Tier == BackupStorageTier.Hot)
            },
            new()
            {
                Name = "Warm",
                SizeGb = Round3(warmGb),
                CostEur = Round4(warmCost),
                Access = "medium",
                Retention = $"≤{StorageTierService.WarmRetentionDays}d",
                ArtifactCount = dumpRows.Count(r => r.Tier == BackupStorageTier.Warm)
            },
            new()
            {
                Name = "Cold",
                SizeGb = Round3(coldGb),
                CostEur = Round4(coldCost),
                Access = "slow",
                Retention = $">{StorageTierService.WarmRetentionDays}d",
                ArtifactCount = dumpRows.Count(r => r.Tier == BackupStorageTier.Cold)
            }
        };

        var recommendations = BuildRecommendations(
            opts,
            usagePct,
            savings,
            hotBytes,
            usedBytes,
            backupCount);

        return new BackupStorageCostResponseDto
        {
            TotalStorageGb = totalGb,
            BudgetGb = budgetGb,
            UsagePercentage = usagePct,
            MonthlyCostEur = monthly,
            CostPerGbEur = Round4(blended),
            BackupCount = backupCount,
            AverageSizeMb = averageMb,
            RetentionSavingsPercent = savings,
            ProjectedMonthlyEur = projected,
            SmartRetentionEnabled = opts.SmartRetentionEnabled,
            StorageTierManagementEnabled = opts.StorageTierManagementEnabled,
            Tiers = tiers,
            Recommendations = recommendations
        };
    }

    private static List<BackupStorageCostRecommendationDto> BuildRecommendations(
        BackupOptions opts,
        double usagePct,
        double savings,
        long hotBytes,
        long usedBytes,
        int backupCount)
    {
        var list = new List<BackupStorageCostRecommendationDto>();

        if (usagePct >= 80)
        {
            list.Add(new BackupStorageCostRecommendationDto
            {
                Code = "storage_pressure",
                Title = "Storage budget nearly full",
                Description =
                    "Succeeded dump budget is above 80%. Reduce retention, enable smart GFS retention, or archive/delete old succeeded runs.",
                SavingsPercent = 15
            });
        }

        if (!opts.SmartRetentionEnabled && backupCount >= 10)
        {
            list.Add(new BackupStorageCostRecommendationDto
            {
                Code = "enable_smart_retention",
                Title = "Enable smart GFS retention",
                Description =
                    "Set Backup:SmartRetentionEnabled=true to thin weekly/monthly/yearly copies instead of keeping every daily dump in the flat window.",
                SavingsPercent = 40
            });
        }

        if (!opts.StorageTierManagementEnabled)
        {
            list.Add(new BackupStorageCostRecommendationDto
            {
                Code = "enable_storage_tiers",
                Title = "Enable storage tier tagging",
                Description =
                    "Set Backup:StorageTierManagementEnabled=true so Hot/Warm/Cold classification drives cost estimates and cold archive preference.",
                SavingsPercent = 20
            });
        }
        else if (usedBytes > 0 && hotBytes > usedBytes * 0.7)
        {
            list.Add(new BackupStorageCostRecommendationDto
            {
                Code = "rebalance_hot",
                Title = "Too much data still on Hot",
                Description =
                    "Most dump bytes are tagged Hot. Confirm tier pass runs after successes, or move aged artifacts toward Warm/Cold (external archive).",
                SavingsPercent = 25
            });
        }

        if (savings < 5 && usedBytes > 0 && opts.StorageTierManagementEnabled)
        {
            list.Add(new BackupStorageCostRecommendationDto
            {
                Code = "tier_savings_low",
                Title = "Limited tier savings so far",
                Description =
                    "Warm/Cold share is low versus Hot pricing. Allow older dumps to age into colder tiers or prefer ExternalArchiveRoot for Cold.",
                SavingsPercent = 10
            });
        }

        if (list.Count == 0)
        {
            list.Add(new BackupStorageCostRecommendationDto
            {
                Code = "healthy",
                Title = "Storage cost profile looks healthy",
                Description =
                    "Budget usage and tier mix are within expected ranges. Keep monitoring after large tenant growth.",
                SavingsPercent = 0
            });
        }

        return list;
    }

    private IQueryable<BackupRun> AccessibleRuns(BackupRunAccessScope? accessScope)
    {
        var q = _db.BackupRuns.AsNoTracking();
        return accessScope == null
            ? q
            : BackupRunAccessEvaluator.ApplyCallerAccessFilter(q, accessScope);
    }

    private static double Round3(double v) => Math.Round(v, 3, MidpointRounding.AwayFromZero);
    private static double Round4(double v) => Math.Round(v, 4, MidpointRounding.AwayFromZero);
}
