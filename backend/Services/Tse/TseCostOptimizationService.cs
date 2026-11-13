using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Estimates TSE operating cost from receipt volume + device inventory using configurable EUR rates.
/// Figures are indicative for Super Admin capacity/cost planning — not invoices or fiscal amounts.
/// </summary>
public sealed class TseCostOptimizationService : ITseCostOptimizationService
{
    private const int MaxPeriodDays = 366;
    private const int DefaultAnomalyLookbackDays = 30;
    private const string BreakdownSigning = "signing";
    private const string BreakdownPrimaryDevices = "primary_devices";
    private const string BreakdownBackupDevices = "backup_devices";
    private const string BreakdownFailoverOverhead = "failover_overhead";

    private readonly AppDbContext _db;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;
    private readonly IActivityEventPublisher _activity;
    private readonly ILogger<TseCostOptimizationService> _logger;

    public TseCostOptimizationService(
        AppDbContext db,
        IOptionsMonitor<TseOptions> tseOptions,
        IActivityEventPublisher activity,
        ILogger<TseCostOptimizationService> logger)
    {
        _db = db;
        _tseOptions = tseOptions;
        _activity = activity;
        _logger = logger;
    }

    public async Task<TseCostReportDto> GetCostReportAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId is required.", nameof(tenantId));

        fromUtc = NormalizeUtc(fromUtc);
        toUtc = NormalizeUtc(toUtc);
        if (toUtc <= fromUtc)
            throw new ArgumentException("toUtc must be strictly greater than fromUtc.", nameof(toUtc));
        if ((toUtc - fromUtc).TotalDays > MaxPeriodDays)
            throw new ArgumentException($"Period must be at most {MaxPeriodDays} days.", nameof(toUtc));

        var tenant = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
            throw new KeyNotFoundException($"Tenant {tenantId} was not found.");

        var opts = _tseOptions.CurrentValue;
        var devices = await LoadTenantDevicesAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var active = devices.Where(d => d.IsActive).ToList();
        var primaries = active.Where(d => d.IsPrimary || d.IsFailoverActive).ToList();
        var backups = active.Where(d => d.IsBackup && !d.IsFailoverActive).ToList();

        var issuedAt = await _db.Receipts.AsNoTracking().IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId
                        && r.IssuedAt >= fromUtc
                        && r.IssuedAt < toUtc)
            .Select(r => new { r.IssuedAt, r.SignatureValue })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var total = issuedAt.Count;
        var signed = issuedAt.Count(r => !string.IsNullOrWhiteSpace(r.SignatureValue));

        var periodDays = Math.Max(1.0, (toUtc - fromUtc).TotalDays);
        var monthFraction = (decimal)(periodDays / 30.0);

        var costPerTx = Math.Max(0m, opts.CostPerSignedTransactionEur);
        var primaryFee = Math.Max(0m, opts.CostMonthlyPrimaryDeviceEur);
        var backupFee = Math.Max(0m, opts.CostMonthlyBackupDeviceEur);
        var failoverFee = Math.Max(0m, opts.CostPerFailoverEventEur);

        var signingCost = RoundMoney(signed * costPerTx);
        var primaryDeviceCost = RoundMoney(primaries.Count * primaryFee * monthFraction);
        var backupDeviceCost = RoundMoney(backups.Count * backupFee * monthFraction);

        var failoverCount = await _db.TseFailoverLogs.AsNoTracking().IgnoreQueryFilters()
            .CountAsync(
                l => l.TenantId == tenantId
                     && l.StartedAt >= fromUtc
                     && l.StartedAt < toUtc
                     && l.IsSuccessful,
                cancellationToken)
            .ConfigureAwait(false);
        var failoverCost = RoundMoney(failoverCount * failoverFee);

        var breakdown = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            [BreakdownSigning] = signingCost,
            [BreakdownPrimaryDevices] = primaryDeviceCost,
            [BreakdownBackupDevices] = backupDeviceCost,
            [BreakdownFailoverOverhead] = failoverCost,
        };

        var totalCost = RoundMoney(breakdown.Values.Sum());
        var avgPerTx = total == 0 ? 0m : RoundMoney(totalCost / total, 4);

        var dailyTrends = BuildDailyTrends(issuedAt.Select(r => r.IssuedAt).ToList(), fromUtc, toUtc, costPerTx);

        var recommendations = BuildRecommendations(
            active,
            primaries,
            backups,
            signed,
            total,
            periodDays,
            opts,
            failoverCount);

        var potentialSavings = RoundMoney(recommendations.Sum(r => r.EstimatedMonthlySavings));

        var (hasAnomaly, anomalyDescription) = DetectInPeriodAnomaly(dailyTrends, opts);

        _logger.LogInformation(
            "TSE cost report TenantId={TenantId} Period={From:o}..{To:o} Total={Total} Cost={Cost} Anomaly={Anomaly}",
            tenantId,
            fromUtc,
            toUtc,
            total,
            totalCost,
            hasAnomaly);

        return new TseCostReportDto
        {
            TenantId = tenantId,
            TenantName = tenant.Name,
            TenantSlug = tenant.Slug,
            PeriodStart = fromUtc,
            PeriodEnd = toUtc,
            GeneratedAt = DateTime.UtcNow,
            TotalTransactions = total,
            SignedTransactions = signed,
            ActiveDeviceCount = active.Count,
            BackupDeviceCount = backups.Count,
            TotalCost = totalCost,
            AverageCostPerTransaction = avgPerTx,
            Currency = "EUR",
            CostBreakdown = breakdown,
            DailyTrends = dailyTrends,
            HasCostAnomaly = hasAnomaly,
            AnomalyDescription = anomalyDescription,
            Recommendations = recommendations,
            PotentialSavings = potentialSavings,
        };
    }

    public async Task<IReadOnlyList<TseCostSavingRecommendationDto>> GetOptimizationRecommendationsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var toUtc = DateTime.UtcNow;
        var fromUtc = toUtc.AddDays(-DefaultAnomalyLookbackDays);
        var report = await GetCostReportAsync(tenantId, fromUtc, toUtc, cancellationToken)
            .ConfigureAwait(false);
        return report.Recommendations;
    }

    public async Task<TseCostAlertDto> CheckCostAnomaliesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId is required.", nameof(tenantId));

        var opts = _tseOptions.CurrentValue;
        var toUtc = DateTime.UtcNow;
        var currentFrom = toUtc.AddDays(-DefaultAnomalyLookbackDays);
        var baselineFrom = currentFrom.AddDays(-DefaultAnomalyLookbackDays);

        TseCostReportDto current;
        TseCostReportDto baseline;
        try
        {
            current = await GetCostReportAsync(tenantId, currentFrom, toUtc, cancellationToken)
                .ConfigureAwait(false);
            baseline = await GetCostReportAsync(tenantId, baselineFrom, currentFrom, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (KeyNotFoundException)
        {
            return new TseCostAlertDto
            {
                TenantId = tenantId,
                HasAnomaly = false,
                Severity = "Info",
                Message = "Tenant not found.",
            };
        }

        var codes = new List<string>();
        var severity = "Info";
        var deltaPercent = 0m;

        if (baseline.TotalCost > 0)
        {
            deltaPercent = RoundMoney(
                ((current.TotalCost - baseline.TotalCost) / baseline.TotalCost) * 100m,
                2);

            if (deltaPercent >= opts.CostAnomalyCriticalIncreasePercent)
            {
                codes.Add("cost_spike_critical");
                severity = "Critical";
            }
            else if (deltaPercent >= opts.CostAnomalyWarningIncreasePercent)
            {
                codes.Add("cost_spike");
                severity = "Warning";
            }
        }
        else if (current.TotalCost > 0 && current.TotalTransactions >= 50)
        {
            // No baseline spend but meaningful new load — informational only.
            codes.Add("cost_baseline_missing");
        }

        if (current.HasCostAnomaly && !codes.Contains("daily_cost_spike"))
        {
            codes.Add("daily_cost_spike");
            severity = MaxSeverity(severity, "Warning");
        }

        var hasAnomaly = codes.Any(c =>
            c is "cost_spike" or "cost_spike_critical" or "daily_cost_spike");

        var message = hasAnomaly
            ? BuildAlertMessage(current, baseline, deltaPercent, codes)
            : current.TotalTransactions == 0
                ? "No TSE-related transactions in the lookback window."
                : "No TSE cost anomalies detected.";

        var alert = new TseCostAlertDto
        {
            TenantId = tenantId,
            HasAnomaly = hasAnomaly,
            Severity = severity,
            Codes = codes,
            Message = message,
            CurrentPeriodCost = current.TotalCost,
            BaselinePeriodCost = baseline.TotalCost,
            CostDeltaPercent = deltaPercent,
            Report = current,
        };

        if (!hasAnomaly)
            return alert;

        await _activity.TryPublishAsync(
                tenantId,
                ActivityEventType.TseCostAnomaly,
                new
                {
                    TenantId = tenantId.ToString("D"),
                    current.TotalCost,
                    BaselineCost = baseline.TotalCost,
                    DeltaPercent = deltaPercent,
                    Codes = codes,
                    Message = message,
                },
                actorUserId: "system",
                dedupKey: $"tse-cost-anomaly:{tenantId:N}:{DateTime.UtcNow:yyyyMMdd}",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        alert.AlertPublished = true;
        return alert;
    }

    private async Task<List<TseDevice>> LoadTenantDevicesAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        return await _db.TseDevices.AsNoTracking().IgnoreQueryFilters()
            .Where(d => d.TenantId == tenantId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static List<TseCostTrendDto> BuildDailyTrends(
        IReadOnlyList<DateTime> issuedAts,
        DateTime fromUtc,
        DateTime toUtc,
        decimal costPerTx)
    {
        var byDay = issuedAts
            .GroupBy(d => DateOnly.FromDateTime(d.Kind == DateTimeKind.Utc ? d : d.ToUniversalTime()))
            .ToDictionary(g => g.Key, g => g.Count());

        var trends = new List<TseCostTrendDto>();
        var cursor = DateOnly.FromDateTime(fromUtc);
        var end = DateOnly.FromDateTime(toUtc.AddTicks(-1));
        if (end < cursor)
            end = cursor;

        for (var day = cursor; day <= end; day = day.AddDays(1))
        {
            byDay.TryGetValue(day, out var count);
            trends.Add(new TseCostTrendDto
            {
                Date = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                TransactionCount = count,
                EstimatedCost = RoundMoney(count * costPerTx),
            });
        }

        return trends;
    }

    private static (bool HasAnomaly, string? Description) DetectInPeriodAnomaly(
        IReadOnlyList<TseCostTrendDto> trends,
        TseOptions opts)
    {
        var activeDays = trends.Where(t => t.TransactionCount > 0).ToList();
        if (activeDays.Count < 3)
            return (false, null);

        var avg = activeDays.Average(t => (double)t.EstimatedCost);
        if (avg <= 0)
            return (false, null);

        var multiplier = Math.Max(1.5, opts.CostDailySpikeMultiplier);
        var spike = activeDays
            .OrderByDescending(t => t.EstimatedCost)
            .FirstOrDefault(t => (double)t.EstimatedCost >= avg * multiplier);

        if (spike is null)
            return (false, null);

        return (
            true,
            $"Daily estimated TSE cost spike on {spike.Date:yyyy-MM-dd}: "
            + $"€{spike.EstimatedCost:0.##} vs average €{avg:0.##} "
            + $"({spike.TransactionCount} transactions).");
    }

    private static List<TseCostSavingRecommendationDto> BuildRecommendations(
        IReadOnlyList<TseDevice> active,
        IReadOnlyList<TseDevice> primaries,
        IReadOnlyList<TseDevice> backups,
        int signed,
        int total,
        double periodDays,
        TseOptions opts,
        int failoverCount)
    {
        var list = new List<TseCostSavingRecommendationDto>();

        if (backups.Count > primaries.Count && backups.Count > 1)
        {
            var excess = backups.Count - Math.Max(1, primaries.Count);
            var savings = excess * Math.Max(0m, opts.CostMonthlyBackupDeviceEur);
            list.Add(new TseCostSavingRecommendationDto
            {
                Code = "reduce_idle_backups",
                Title = "Reduce idle backup TSE devices",
                Description =
                    $"{excess} active backup device(s) exceed primary count. "
                    + "Keep one healthy backup per primary and deactivate unused spares.",
                Severity = "Warning",
                EstimatedMonthlySavings = savings,
            });
        }

        if (primaries.Count > 1)
        {
            var avgSignedPerPrimary = signed / (double)primaries.Count;
            var days = Math.Max(1.0, periodDays);
            var dailyPerPrimary = avgSignedPerPrimary / days;
            if (dailyPerPrimary < opts.CostLowUtilizationDailyTxThreshold)
            {
                var consolidateCount = primaries.Count - 1;
                var savings = consolidateCount * Math.Max(0m, opts.CostMonthlyPrimaryDeviceEur);
                list.Add(new TseCostSavingRecommendationDto
                {
                    Code = "consolidate_low_utilization",
                    Title = "Consolidate low-utilization primary TSE devices",
                    Description =
                        $"Average ~{dailyPerPrimary:0.#} signed tx/day per primary is below the "
                        + $"{opts.CostLowUtilizationDailyTxThreshold} threshold. "
                        + "Consider fewer primary devices if registers can share capacity.",
                    Severity = "Info",
                    EstimatedMonthlySavings = savings,
                });
            }
        }

        if (failoverCount >= opts.CostHighFailoverCountThreshold)
        {
            list.Add(new TseCostSavingRecommendationDto
            {
                Code = "investigate_failover_churn",
                Title = "Investigate frequent TSE failovers",
                Description =
                    $"{failoverCount} successful failover(s) in the period add overhead and may "
                    + "indicate unstable primary hardware. Stabilizing reduces dual-device cost pressure.",
                Severity = "Warning",
                EstimatedMonthlySavings = RoundMoney(
                    failoverCount * Math.Max(0m, opts.CostPerFailoverEventEur)),
            });
        }

        var softOrDemo = active.Count(d =>
            string.Equals(d.DeviceType, "Soft", StringComparison.OrdinalIgnoreCase)
            || string.Equals(d.Provider, TseOptions.ProviderSoft, StringComparison.OrdinalIgnoreCase)
            || string.Equals(d.Provider, TseOptions.ProviderFake, StringComparison.OrdinalIgnoreCase));
        var cloud = active.Count - softOrDemo;
        if (opts.IsFakeSigningMode && cloud > 0)
        {
            list.Add(new TseCostSavingRecommendationDto
            {
                Code = "prefer_soft_in_fake_mode",
                Title = "Prefer Soft/Fake TSE while signing mode is Fake",
                Description =
                    "Deployment is in Fake signing mode but cloud/hardware devices remain active. "
                    + "Use Soft TSE in non-production to avoid unnecessary vendor fees.",
                Severity = "Info",
                EstimatedMonthlySavings = RoundMoney(
                    cloud * Math.Max(0m, opts.CostMonthlyPrimaryDeviceEur) * 0.5m),
            });
        }

        if (total > 0 && signed < total)
        {
            var unsigned = total - signed;
            list.Add(new TseCostSavingRecommendationDto
            {
                Code = "fix_unsigned_receipts",
                Title = "Reduce unsigned fiscal receipts",
                Description =
                    $"{unsigned} receipt(s) lack a TSE signature. Fixing signing avoids rework "
                    + "and compliance follow-up cost (not a direct vendor fee).",
                Severity = "Warning",
                EstimatedMonthlySavings = 0m,
            });
        }

        if (list.Count == 0)
        {
            list.Add(new TseCostSavingRecommendationDto
            {
                Code = "monitor",
                Title = "No major cost optimizations identified",
                Description =
                    "Indicative TSE cost profile looks balanced for the current device inventory and volume.",
                Severity = "Info",
                EstimatedMonthlySavings = 0m,
            });
        }

        return list;
    }

    private static string BuildAlertMessage(
        TseCostReportDto current,
        TseCostReportDto baseline,
        decimal deltaPercent,
        IReadOnlyList<string> codes)
    {
        var parts = new List<string>();
        if (codes.Any(c => c.StartsWith("cost_spike", StringComparison.Ordinal)))
        {
            parts.Add(
                $"Period cost €{current.TotalCost:0.##} vs baseline €{baseline.TotalCost:0.##} "
                + $"({deltaPercent:+0.##;-0.##;0}%).");
        }

        if (codes.Contains("daily_cost_spike") && !string.IsNullOrWhiteSpace(current.AnomalyDescription))
            parts.Add(current.AnomalyDescription);

        return string.Join(' ', parts);
    }

    private static string MaxSeverity(string current, string candidate)
    {
        static int Rank(string s) => s switch
        {
            "Critical" => 3,
            "Warning" => 2,
            _ => 1,
        };

        return Rank(candidate) > Rank(current) ? candidate : current;
    }

    private static decimal RoundMoney(decimal value, int decimals = 2) =>
        Math.Round(value, decimals, MidpointRounding.AwayFromZero);

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
}
