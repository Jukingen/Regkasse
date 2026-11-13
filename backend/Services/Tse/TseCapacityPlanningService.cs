using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Daily receipt trends, capacity headroom, growth forecast, and near-capacity alerts.
/// </summary>
public sealed class TseCapacityPlanningService : ITseCapacityPlanningService
{
    private const int MinLookbackDays = 7;
    private const int MaxLookbackDays = 90;
    private const int MaxForecastDays = 90;

    private readonly AppDbContext _db;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;
    private readonly IActivityEventPublisher _activity;
    private readonly ILogger<TseCapacityPlanningService> _logger;

    public TseCapacityPlanningService(
        AppDbContext db,
        IOptionsMonitor<TseOptions> tseOptions,
        IActivityEventPublisher activity,
        ILogger<TseCapacityPlanningService> logger)
    {
        _db = db;
        _tseOptions = tseOptions;
        _activity = activity;
        _logger = logger;
    }

    public async Task<TseCapacityReportDto> GetCapacityReportAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId is required.", nameof(tenantId));

        var tenant = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
            throw new KeyNotFoundException($"Tenant {tenantId} was not found.");

        var opts = _tseOptions.CurrentValue;
        var lookbackDays = Math.Clamp(opts.CapacityLookbackDays, MinLookbackDays, MaxLookbackDays);
        var toUtc = DateTime.UtcNow;
        var fromUtc = toUtc.Date.AddDays(-(lookbackDays - 1));

        var rows = await _db.Receipts.AsNoTracking().IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId
                        && r.IssuedAt >= fromUtc
                        && r.IssuedAt < toUtc)
            .Select(r => new ReceiptRow(r.IssuedAt, r.SignatureValue))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var signingDevices = await _db.TseDevices.AsNoTracking().IgnoreQueryFilters()
            .CountAsync(
                d => d.TenantId == tenantId
                     && d.IsActive
                     && (d.IsPrimary || d.IsFailoverActive),
                cancellationToken)
            .ConfigureAwait(false);

        var deviceCount = Math.Max(1, signingDevices);
        var maxDaily = Math.Max(1, opts.CapacityPerDevicePerDay) * deviceCount;
        var maxHourly = Math.Max(1, opts.CapacityPerDevicePerHour) * deviceCount;

        var trends = BuildDailyTrends(rows, fromUtc, toUtc.Date);
        var total = rows.Count;
        var dailyAvg = (int)Math.Round(trends.Count == 0 ? 0 : trends.Average(t => t.TransactionCount));
        var peakHourly = rows.Count == 0
            ? 0
            : rows.GroupBy(r => new DateTime(
                    r.IssuedAt.Year, r.IssuedAt.Month, r.IssuedAt.Day, r.IssuedAt.Hour, 0, 0, DateTimeKind.Utc))
                .Max(g => g.Count());

        var growth = ComputeDailyGrowthPercent(trends);
        var utilization = Round2(100.0 * dailyAvg / maxDaily);
        var warningPct = Clamp(opts.CapacityWarningUtilizationPercent, 50, 99);
        var criticalPct = Clamp(opts.CapacityCriticalUtilizationPercent, warningPct, 100);
        var near = utilization >= warningPct;

        DateTime? reachDate = null;
        if (growth > 0.01 && dailyAvg > 0 && dailyAvg < maxDaily)
        {
            var daysUntil = 0;
            var projected = (double)dailyAvg;
            while (projected < maxDaily && daysUntil < 3650)
            {
                projected *= 1.0 + growth / 100.0;
                daysUntil++;
            }

            if (projected >= maxDaily)
                reachDate = toUtc.Date.AddDays(daysUntil);
        }

        var nextMonth = (int)Math.Round(dailyAvg * 30.0 * (1.0 + Math.Max(0, growth) / 100.0));
        var recommendations = BuildRecommendations(
            utilization,
            warningPct,
            criticalPct,
            growth,
            peakHourly,
            maxHourly,
            signingDevices,
            reachDate);

        return new TseCapacityReportDto
        {
            TenantId = tenantId,
            TenantName = tenant.Name,
            TenantSlug = tenant.Slug,
            GeneratedAt = DateTime.UtcNow,
            PeriodStart = fromUtc,
            PeriodEnd = toUtc,
            DailyTransactionAverage = dailyAvg,
            MonthlyTransactionTotal = total,
            PeakHourlyTransactions = peakHourly,
            ActiveSigningDevices = signingDevices,
            MaxDailyCapacity = maxDaily,
            MaxHourlyCapacity = maxHourly,
            CurrentUtilizationPercentage = utilization,
            EstimatedNextMonthTransactions = nextMonth,
            EstimatedCapacityReachDate = reachDate,
            IsNearCapacity = near,
            LookbackDays = lookbackDays,
            DailyGrowthRatePercent = Round2(growth),
            DailyTrends = trends,
            Recommendations = recommendations,
        };
    }

    public async Task<TseForecastResultDto> ForecastCapacityAsync(
        Guid tenantId,
        int forecastDays = 30,
        CancellationToken cancellationToken = default)
    {
        forecastDays = Math.Clamp(forecastDays, 1, MaxForecastDays);
        var report = await GetCapacityReportAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var growth = report.DailyGrowthRatePercent / 100.0;
        var baseline = Math.Max(0, report.DailyTransactionAverage);
        var start = DateTime.UtcNow.Date.AddDays(1);
        var points = new List<TseForecastDayPointDto>(forecastDays);
        double running = baseline;
        var total = 0;

        for (var i = 0; i < forecastDays; i++)
        {
            running = Math.Max(0, running * (1.0 + growth));
            var dayCount = (int)Math.Round(running);
            total += dayCount;
            points.Add(new TseForecastDayPointDto
            {
                Date = DateTime.SpecifyKind(start.AddDays(i), DateTimeKind.Utc),
                EstimatedTransactions = dayCount,
            });
        }

        var nonZeroDays = report.DailyTrends.Count(t => t.TransactionCount > 0);
        var confidence = report.LookbackDays >= 21 && nonZeroDays >= 14
            ? "High"
            : report.LookbackDays >= 14
                ? "Medium"
                : "Low";

        var peakFactor = report.DailyTransactionAverage > 0
            ? (double)report.PeakHourlyTransactions / report.DailyTransactionAverage
            : 0.15;

        return new TseForecastResultDto
        {
            TenantId = tenantId,
            ForecastDays = forecastDays,
            GeneratedAt = DateTime.UtcNow,
            BaselineDailyAverage = baseline,
            EstimatedTotalTransactions = total,
            EstimatedDailyAverage = (int)Math.Round(total / (double)forecastDays),
            EstimatedPeakHourly = (int)Math.Round(running * Math.Clamp(peakFactor, 0.05, 0.5)),
            DailyGrowthRatePercent = report.DailyGrowthRatePercent,
            Confidence = confidence,
            DailyPoints = points,
        };
    }

    public async Task<TseCapacityAlertDto> CheckCapacityAlertsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId is required.", nameof(tenantId));

        TseCapacityReportDto report;
        try
        {
            report = await GetCapacityReportAsync(tenantId, cancellationToken).ConfigureAwait(false);
        }
        catch (KeyNotFoundException)
        {
            return new TseCapacityAlertDto
            {
                TenantId = tenantId,
                HasAlert = false,
                Severity = "Info",
                Message = "Tenant not found.",
            };
        }

        var opts = _tseOptions.CurrentValue;
        var warningPct = Clamp(opts.CapacityWarningUtilizationPercent, 50, 99);
        var criticalPct = Clamp(opts.CapacityCriticalUtilizationPercent, warningPct, 100);
        var codes = new List<string>();
        var severity = "Info";

        if (report.CurrentUtilizationPercentage >= criticalPct)
        {
            codes.Add("capacity_critical");
            severity = "Critical";
        }
        else if (report.CurrentUtilizationPercentage >= warningPct)
        {
            codes.Add("capacity_warning");
            severity = "Warning";
        }

        if (report.PeakHourlyTransactions >= report.MaxHourlyCapacity)
        {
            codes.Add("hourly_capacity_exceeded");
            severity = MaxSeverity(severity, "Critical");
        }
        else if (report.MaxHourlyCapacity > 0
                 && report.PeakHourlyTransactions >= report.MaxHourlyCapacity * 0.9)
        {
            codes.Add("hourly_capacity_warning");
            severity = MaxSeverity(severity, "Warning");
        }

        if (report.EstimatedCapacityReachDate is { } reach
            && reach <= DateTime.UtcNow.Date.AddDays(Math.Clamp(opts.CapacityReachAlertDays, 7, 365)))
        {
            codes.Add("capacity_reach_soon");
            severity = MaxSeverity(severity, "Warning");
        }

        var hasAlert = codes.Count > 0;
        var message = hasAlert
            ? $"TSE capacity alert ({string.Join(", ", codes)}): utilization {report.CurrentUtilizationPercentage:0.##}%."
            : "TSE capacity within configured thresholds.";

        var alert = new TseCapacityAlertDto
        {
            TenantId = tenantId,
            HasAlert = hasAlert,
            IsNearCapacity = report.IsNearCapacity,
            Severity = severity,
            Codes = codes,
            Message = message,
            UtilizationPercentage = report.CurrentUtilizationPercentage,
            EstimatedCapacityReachDate = report.EstimatedCapacityReachDate,
            Report = report,
        };

        if (!hasAlert)
            return alert;

        await _activity.TryPublishAsync(
                tenantId,
                ActivityEventType.TseCapacityNearLimit,
                new
                {
                    TenantId = tenantId.ToString("D"),
                    report.CurrentUtilizationPercentage,
                    report.DailyTransactionAverage,
                    report.MaxDailyCapacity,
                    report.EstimatedCapacityReachDate,
                    Codes = codes,
                    Severity = severity,
                    Message = message,
                },
                actorUserId: "system",
                dedupKey: $"tse-capacity:{tenantId:N}:{DateTime.UtcNow:yyyyMMddHH}",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        alert.AlertPublished = true;
        _logger.LogWarning(
            "TSE capacity alert TenantId={TenantId} Utilization={Utilization} Severity={Severity}",
            tenantId,
            report.CurrentUtilizationPercentage,
            severity);

        return alert;
    }

    private static List<TseDailyTransactionTrendDto> BuildDailyTrends(
        IReadOnlyList<ReceiptRow> rows,
        DateTime fromDate,
        DateTime lastInclusiveDate)
    {
        var byDay = rows
            .GroupBy(r => r.IssuedAt.Date)
            .ToDictionary(
                g => g.Key,
                g => (
                    Total: g.Count(),
                    Signed: g.Count(x => !string.IsNullOrWhiteSpace(x.SignatureValue))));

        var trends = new List<TseDailyTransactionTrendDto>();
        for (var d = fromDate.Date; d <= lastInclusiveDate; d = d.AddDays(1))
        {
            byDay.TryGetValue(d, out var counts);
            trends.Add(new TseDailyTransactionTrendDto
            {
                Date = DateTime.SpecifyKind(d, DateTimeKind.Utc),
                TransactionCount = counts.Total,
                SignedCount = counts.Signed,
            });
        }

        return trends;
    }

    private static double ComputeDailyGrowthPercent(IReadOnlyList<TseDailyTransactionTrendDto> trends)
    {
        if (trends.Count < 4)
            return 0;

        var half = trends.Count / 2;
        var first = trends.Take(half).Average(t => t.TransactionCount);
        var second = trends.Skip(half).Average(t => t.TransactionCount);
        if (first < 0.5)
            return second > first ? 5.0 : 0;
        return ((second - first) / first) * 100.0 / Math.Max(1, half);
    }

    private static IReadOnlyList<string> BuildRecommendations(
        double utilization,
        double warningPct,
        double criticalPct,
        double growth,
        int peakHourly,
        int maxHourly,
        int signingDevices,
        DateTime? reachDate)
    {
        var list = new List<string>();
        if (utilization >= criticalPct)
            list.Add("Critical utilization — provision additional primary TSE capacity or redistribute load.");
        else if (utilization >= warningPct)
            list.Add("Approaching daily capacity — review device inventory and peak-hour routing.");

        if (peakHourly >= maxHourly * 0.9)
            list.Add("Peak hourly volume is near device hourly capacity — consider staggered closings or extra devices.");

        if (growth >= 2)
            list.Add("Sustained daily growth detected — schedule capacity review for next month.");

        if (reachDate is { } d && d <= DateTime.UtcNow.Date.AddDays(90))
            list.Add($"At current growth, estimated capacity reach date is {d:yyyy-MM-dd}.");

        if (signingDevices <= 1 && utilization >= warningPct * 0.7)
            list.Add("Only one active signing device — add a backup/primary for headroom and failover.");

        if (list.Count == 0)
            list.Add("Capacity headroom is healthy for the current lookback window.");

        return list;
    }

    private static string MaxSeverity(string a, string b)
    {
        static int Rank(string s) => s switch
        {
            "Critical" => 3,
            "Warning" => 2,
            "Info" => 1,
            _ => 0,
        };
        return Rank(a) >= Rank(b) ? a : b;
    }

    private static double Clamp(double value, double min, double max) =>
        Math.Min(max, Math.Max(min, value));

    private static double Round2(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed record ReceiptRow(DateTime IssuedAt, string? SignatureValue);
}
