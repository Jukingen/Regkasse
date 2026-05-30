using System.Globalization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public interface IPaymentTrendAnalysisService
{
    Task<TrendAnalysisResponse> GetTrendAnalysisAsync(
        Guid tenantId,
        TrendPeriod period,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default);
}

public sealed class PaymentTrendAnalysisService : IPaymentTrendAnalysisService
{
    private const decimal StableGrowthThresholdPercent = 1m;
    private readonly AppDbContext _context;

    public PaymentTrendAnalysisService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<TrendAnalysisResponse> GetTrendAnalysisAsync(
        Guid tenantId,
        TrendPeriod period,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default)
    {
        var (fromUtc, toExclusiveUtc) = ResolveUtcRange(period, startDate, endDate);
        var currentRows = await LoadSuccessfulPaymentsAsync(tenantId, fromUtc, toExclusiveUtc, cancellationToken);

        var trendData = GroupPaymentsByPeriod(currentRows, period);
        var comparison = await BuildComparisonAsync(
            tenantId,
            period,
            fromUtc,
            toExclusiveUtc,
            currentRows,
            cancellationToken);

        return new TrendAnalysisResponse
        {
            Period = period,
            StartDate = fromUtc,
            EndDate = toExclusiveUtc,
            TrendData = trendData,
            Comparison = comparison,
            Summary = CalculateSummary(currentRows, trendData),
        };
    }

    private static (DateTime FromUtc, DateTime ToExclusiveUtc) ResolveUtcRange(
        TrendPeriod period,
        DateTime? startDate,
        DateTime? endDate)
    {
        if (startDate.HasValue || endDate.HasValue)
        {
            var startCal = startDate ?? endDate!.Value;
            var endCal = endDate ?? startDate!.Value;
            return PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(startCal, endCal);
        }

        var endUtc = DateTime.UtcNow;
        var startUtc = period switch
        {
            TrendPeriod.Daily => endUtc.AddDays(-30),
            TrendPeriod.Weekly => endUtc.AddDays(-90),
            TrendPeriod.Monthly => endUtc.AddMonths(-12),
            _ => endUtc.AddDays(-30),
        };
        return (startUtc, endUtc);
    }

    private async Task<List<PaymentTrendRow>> LoadSuccessfulPaymentsAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toExclusiveUtc,
        CancellationToken cancellationToken)
    {
        return await _context.PaymentDetails
            .AsNoTracking()
            .ApplyTenantCashRegisterScope(_context.CashRegisters.AsNoTracking(), tenantId)
            .Where(p => p.CreatedAt >= fromUtc && p.CreatedAt < toExclusiveUtc)
            .Where(p => p.IsActive && !p.IsStorno && !p.IsRefund)
            .Select(p => new PaymentTrendRow(p.CreatedAt, p.TotalAmount, p.PaymentMethodRaw))
            .ToListAsync(cancellationToken);
    }

    private static List<TrendDataPoint> GroupPaymentsByPeriod(
        IReadOnlyList<PaymentTrendRow> payments,
        TrendPeriod period)
    {
        if (payments.Count == 0)
            return [];

        return period switch
        {
            TrendPeriod.Daily => payments
                .GroupBy(p => ViennaLocalDate(p.CreatedAtUtc))
                .Select(g => new TrendDataPoint
                {
                    Date = g.Key,
                    TotalAmount = g.Sum(p => p.TotalAmount),
                    TransactionCount = g.Count(),
                    AverageAmount = g.Average(p => p.TotalAmount),
                    Label = g.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                })
                .OrderBy(d => d.Date)
                .ToList(),

            TrendPeriod.Weekly => payments
                .GroupBy(p => WeekStartMonday(ViennaLocalDate(p.CreatedAtUtc)))
                .Select(g =>
                {
                    var weekStart = g.Key;
                    var isoWeek = ISOWeek.GetWeekOfYear(weekStart);
                    return new TrendDataPoint
                    {
                        Date = weekStart,
                        TotalAmount = g.Sum(p => p.TotalAmount),
                        TransactionCount = g.Count(),
                        AverageAmount = g.Average(p => p.TotalAmount),
                        WeekNumber = isoWeek,
                        Label = $"KW {isoWeek}",
                    };
                })
                .OrderBy(d => d.Date)
                .ToList(),

            TrendPeriod.Monthly => payments
                .GroupBy(p =>
                {
                    var local = ViennaLocalDate(p.CreatedAtUtc);
                    return new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
                })
                .Select(g => new TrendDataPoint
                {
                    Date = g.Key,
                    TotalAmount = g.Sum(p => p.TotalAmount),
                    TransactionCount = g.Count(),
                    AverageAmount = g.Average(p => p.TotalAmount),
                    Label = g.Key.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("de-AT")),
                })
                .OrderBy(d => d.Date)
                .ToList(),

            _ => [],
        };
    }

    private async Task<ComparisonData> BuildComparisonAsync(
        Guid tenantId,
        TrendPeriod period,
        DateTime fromUtc,
        DateTime toExclusiveUtc,
        IReadOnlyList<PaymentTrendRow> currentRows,
        CancellationToken cancellationToken)
    {
        var duration = toExclusiveUtc - fromUtc;
        if (duration <= TimeSpan.Zero)
            duration = TimeSpan.FromDays(1);

        var previousToExclusive = fromUtc;
        var previousFrom = previousToExclusive - duration;
        var previousRows = await LoadSuccessfulPaymentsAsync(
            tenantId,
            previousFrom,
            previousToExclusive,
            cancellationToken);

        var currentTotal = currentRows.Sum(p => p.TotalAmount);
        var previousTotal = previousRows.Sum(p => p.TotalAmount);
        var growth = CalculateGrowthPercentage(previousTotal, currentTotal);

        return new ComparisonData
        {
            PreviousPeriodTotal = previousTotal,
            CurrentPeriodTotal = currentTotal,
            GrowthPercentage = growth,
            Trend = ResolveTrendDirection(growth),
            PaymentMethodComparison = BuildPaymentMethodComparison(currentRows, previousRows),
        };
    }

    private static TrendSummary CalculateSummary(
        IReadOnlyList<PaymentTrendRow> payments,
        IReadOnlyList<TrendDataPoint> trendData)
    {
        if (payments.Count == 0)
        {
            return new TrendSummary();
        }

        var totalRevenue = payments.Sum(p => p.TotalAmount);
        var totalTransactions = payments.Count;

        var dailyBuckets = payments
            .GroupBy(p => ViennaLocalDate(p.CreatedAtUtc))
            .Select(g => new { Date = g.Key, Total = g.Sum(p => p.TotalAmount) })
            .OrderByDescending(x => x.Total)
            .FirstOrDefault();

        var methodGroups = payments
            .GroupBy(p => AdminPaymentListMapper.ParsePaymentMethodName(p.PaymentMethodRaw))
            .Select(g => new { Method = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .FirstOrDefault();

        var peakHourGroup = payments
            .GroupBy(p => ViennaLocalDateTime(p.CreatedAtUtc).Hour)
            .Select(g => new { Hour = g.Key, Total = g.Sum(p => p.TotalAmount) })
            .OrderByDescending(x => x.Total)
            .FirstOrDefault();

        return new TrendSummary
        {
            TotalRevenue = totalRevenue,
            TotalTransactions = totalTransactions,
            AverageTransactionValue = totalTransactions == 0 ? 0 : totalRevenue / totalTransactions,
            BestDay = dailyBuckets?.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            BestDayRevenue = dailyBuckets?.Total ?? 0,
            MostUsedPaymentMethod = methodGroups?.Method,
            PeakHourRevenue = peakHourGroup?.Total ?? 0,
            PeakHour = peakHourGroup?.Hour ?? 0,
        };
    }

    private static IReadOnlyList<PaymentMethodComparison> BuildPaymentMethodComparison(
        IReadOnlyList<PaymentTrendRow> currentRows,
        IReadOnlyList<PaymentTrendRow> previousRows)
    {
        var currentByMethod = currentRows
            .GroupBy(p => AdminPaymentListMapper.ParsePaymentMethodName(p.PaymentMethodRaw))
            .ToDictionary(g => g.Key, g => g.Sum(p => p.TotalAmount));

        var previousByMethod = previousRows
            .GroupBy(p => AdminPaymentListMapper.ParsePaymentMethodName(p.PaymentMethodRaw))
            .ToDictionary(g => g.Key, g => g.Sum(p => p.TotalAmount));

        var methods = currentByMethod.Keys.Union(previousByMethod.Keys, StringComparer.OrdinalIgnoreCase);
        return methods
            .Select(method =>
            {
                currentByMethod.TryGetValue(method, out var currentAmount);
                previousByMethod.TryGetValue(method, out var previousAmount);
                return new PaymentMethodComparison
                {
                    Method = method,
                    CurrentAmount = currentAmount,
                    PreviousAmount = previousAmount,
                    ChangePercentage = CalculateGrowthPercentage(previousAmount, currentAmount),
                };
            })
            .OrderByDescending(x => x.CurrentAmount)
            .ToList();
    }

    private static decimal CalculateGrowthPercentage(decimal previous, decimal current)
    {
        if (previous == 0m)
            return current > 0 ? 100m : 0m;

        return Math.Round((current - previous) / previous * 100m, 2);
    }

    private static string ResolveTrendDirection(decimal growthPercentage)
    {
        if (growthPercentage > StableGrowthThresholdPercent)
            return "up";
        if (growthPercentage < -StableGrowthThresholdPercent)
            return "down";
        return "stable";
    }

    private static DateTime ViennaLocalDate(DateTime utcInstant)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(
            PostgreSqlUtcDateTime.InstantToPersistUtc(utcInstant),
            PostgreSqlUtcDateTime.AustriaTimeZone);
        return local.Date;
    }

    private static DateTime ViennaLocalDateTime(DateTime utcInstant) =>
        TimeZoneInfo.ConvertTimeFromUtc(
            PostgreSqlUtcDateTime.InstantToPersistUtc(utcInstant),
            PostgreSqlUtcDateTime.AustriaTimeZone);

    private static DateTime WeekStartMonday(DateTime viennaLocalDate)
    {
        var diff = (7 + (viennaLocalDate.DayOfWeek - DayOfWeek.Monday)) % 7;
        return viennaLocalDate.AddDays(-diff);
    }

    private sealed record PaymentTrendRow(DateTime CreatedAtUtc, decimal TotalAmount, string PaymentMethodRaw);
}
