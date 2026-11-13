using System.Globalization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Constants;
using KasseAPI_Final.Services.Caching;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Analytics;

/// <summary>Super Admin cross-tenant POS GMV. Does not mix with license_sales MRR.</summary>
public sealed class PaymentVolumeAnalyticsService : IPaymentVolumeAnalyticsService
{
    public static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);
    private const int MaxRangeDays = 366;

    private readonly AppDbContext _db;
    private readonly ICacheService _cache;
    private readonly ILogger<PaymentVolumeAnalyticsService> _logger;

    public PaymentVolumeAnalyticsService(
        AppDbContext db,
        ICacheService cache,
        ILogger<PaymentVolumeAnalyticsService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public Task<PaymentVolumeAnalyticsDto> GetPaymentVolumeAnalyticsAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string groupBy = "month",
        CancellationToken cancellationToken = default)
    {
        var granularity = NormalizeGroupBy(groupBy);
        var (from, toExclusive) = NormalizeRange(fromUtc, toUtc, granularity);
        var cacheKey = CacheKeys.Format(
            CacheKeys.PaymentVolumeAnalytics,
            granularity,
            from.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            toExclusive.ToString("yyyyMMdd", CultureInfo.InvariantCulture));

        return _cache.GetOrCreateAsync(
            cacheKey,
            ct => LoadAsync(from, toExclusive, ct),
            CacheTtl,
            cancellationToken);
    }

    internal static string NormalizeGroupBy(string? groupBy)
    {
        if (string.Equals(groupBy, "day", StringComparison.OrdinalIgnoreCase))
            return "day";
        if (string.Equals(groupBy, "week", StringComparison.OrdinalIgnoreCase))
            return "week";
        return "month";
    }

    internal static (DateTime FromUtc, DateTime ToExclusiveUtc) NormalizeRange(
        DateTime? fromUtc,
        DateTime? toUtc,
        string groupBy)
    {
        var now = DateTime.UtcNow;
        var toExclusive = toUtc?.ToUniversalTime() ?? now;
        DateTime from;
        if (fromUtc.HasValue)
            from = fromUtc.Value.ToUniversalTime();
        else
        {
            from = groupBy switch
            {
                "day" => toExclusive.AddDays(-30),
                "week" => toExclusive.AddDays(-90),
                _ => toExclusive.AddMonths(-12),
            };
        }

        if (from > toExclusive)
            (from, toExclusive) = (toExclusive, from);

        if ((toExclusive - from).TotalDays > MaxRangeDays)
            from = toExclusive.AddDays(-MaxRangeDays);

        return (from, toExclusive);
    }

    internal static decimal CalculateMonthlyGrowth(decimal thisMonth, decimal lastMonth)
    {
        if (lastMonth == 0m)
            return thisMonth == 0m ? 0m : 100m;

        return decimal.Round((thisMonth - lastMonth) / lastMonth * 100m, 2, MidpointRounding.AwayFromZero);
    }

    private async Task<PaymentVolumeAnalyticsDto> LoadAsync(
        DateTime fromUtc,
        DateTime toExclusiveUtc,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var thisMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonthStart = thisMonthStart.AddMonths(-1);
        var nextMonthStart = thisMonthStart.AddMonths(1);

        var platformId = SystemTenantIds.Platform;
        var registerIds = await _db.CashRegisters
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(r => r.TenantId != platformId)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var registerSet = registerIds.ToHashSet();

        var chartFrom = fromUtc < lastMonthStart ? fromUtc : lastMonthStart;
        var rows = await _db.PaymentDetails
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(p => p.IsActive && !p.IsStorno && !p.IsRefund)
            .Where(p => p.CreatedAt >= chartFrom && p.CreatedAt < toExclusiveUtc)
            .Select(p => new { p.CashRegisterId, p.CreatedAt, p.TotalAmount })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        rows = rows.Where(p => registerSet.Contains(p.CashRegisterId)).ToList();

        var inRange = rows.Where(p => p.CreatedAt >= fromUtc && p.CreatedAt < toExclusiveUtc).ToList();
        var totalRevenue = decimal.Round(inRange.Sum(p => p.TotalAmount), 2, MidpointRounding.AwayFromZero);
        var totalTx = inRange.Count;
        var avg = totalTx > 0
            ? decimal.Round(totalRevenue / totalTx, 2, MidpointRounding.AwayFromZero)
            : 0m;

        var thisMonthRows = rows.Where(p => p.CreatedAt >= thisMonthStart && p.CreatedAt < nextMonthStart).ToList();
        var lastMonthRows = rows.Where(p => p.CreatedAt >= lastMonthStart && p.CreatedAt < thisMonthStart).ToList();
        var revenueThisMonth = decimal.Round(thisMonthRows.Sum(p => p.TotalAmount), 2, MidpointRounding.AwayFromZero);
        var revenueLastMonth = decimal.Round(lastMonthRows.Sum(p => p.TotalAmount), 2, MidpointRounding.AwayFromZero);

        var dailyFrom = fromUtc.Date;
        var dailyTo = toExclusiveUtc.Date;
        if ((dailyTo - dailyFrom).TotalDays > 90)
            dailyFrom = dailyTo.AddDays(-90);

        var dailyMap = inRange
            .GroupBy(p => p.CreatedAt.Date)
            .ToDictionary(g => g.Key, g => (Revenue: g.Sum(x => x.TotalAmount), Count: g.Count()));

        var daily = new List<DailyVolumeDto>();
        for (var d = dailyFrom; d <= dailyTo && d <= now.Date; d = d.AddDays(1))
        {
            dailyMap.TryGetValue(d, out var bucket);
            daily.Add(new DailyVolumeDto(
                d,
                decimal.Round(bucket.Revenue, 2, MidpointRounding.AwayFromZero),
                bucket.Count));
        }

        var monthlyMap = inRange
            .GroupBy(p => new DateTime(p.CreatedAt.Year, p.CreatedAt.Month, 1, 0, 0, 0, DateTimeKind.Utc))
            .ToDictionary(g => g.Key, g => (Revenue: g.Sum(x => x.TotalAmount), Count: g.Count()));

        var monthCursor = new DateTime(fromUtc.Year, fromUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = new DateTime(toExclusiveUtc.Year, toExclusiveUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthly = new List<MonthlyVolumeDto>();
        for (; monthCursor <= monthEnd; monthCursor = monthCursor.AddMonths(1))
        {
            monthlyMap.TryGetValue(monthCursor, out var bucket);
            monthly.Add(new MonthlyVolumeDto(
                monthCursor.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                decimal.Round(bucket.Revenue, 2, MidpointRounding.AwayFromZero),
                bucket.Count));
        }

        var dto = new PaymentVolumeAnalyticsDto(
            TotalRevenue: totalRevenue,
            RevenueThisMonth: revenueThisMonth,
            RevenueLastMonth: revenueLastMonth,
            MonthlyGrowth: CalculateMonthlyGrowth(revenueThisMonth, revenueLastMonth),
            TotalTransactions: totalTx,
            TransactionsThisMonth: thisMonthRows.Count,
            TransactionsLastMonth: lastMonthRows.Count,
            AverageTransactionValue: avg,
            DailyVolume: daily,
            MonthlyVolume: monthly);

        _logger.LogDebug(
            "Payment volume analytics: revenue={Revenue} tx={Tx} growth={Growth}",
            dto.TotalRevenue,
            dto.TotalTransactions,
            dto.MonthlyGrowth);

        return dto;
    }
}
