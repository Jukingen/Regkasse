using System.Globalization;
using System.Text;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class FiskalyStatisticsService : IFiskalyStatisticsService
{
    public const int MaxRangeDays = 366;
    public const int DefaultLookbackDays = 30;

    private readonly AppDbContext _db;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public FiskalyStatisticsService(AppDbContext db, ICurrentTenantAccessor tenantAccessor)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
    }

    public async Task<FiskalyStatisticsDto> GetAsync(
        FiskalyStatisticsQuery query,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        var (fromUtc, toUtc) = ResolveRange(query.FromUtc, query.ToUtc);
        var source = ApplyFilters(BaseQuery(actorIsSuperAdmin), query, actorIsSuperAdmin, fromUtc, toUtc);

        var rows = await source
            .Select(x => new StatRow(x.CreatedAtUtc, x.CompletedAtUtc, x.Status, x.OperationType))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Build(fromUtc, toUtc, actorIsSuperAdmin ? query.TenantId : _tenantAccessor.TenantId, rows);
    }

    public async Task<(byte[] Bytes, string ContentType, string FileName)> ExportAsync(
        FiskalyStatisticsQuery query,
        string format,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        var stats = await GetAsync(query, actorIsSuperAdmin, cancellationToken).ConfigureAwait(false);
        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + "Z";
        var kind = (format ?? "csv").Trim().ToLowerInvariant();
        if (kind is "pdf")
        {
            return (
                FiskalyStatisticsPdfGenerator.Generate(stats),
                "application/pdf",
                $"fiskaly-statistics_{stamp}.pdf");
        }

        if (kind is not "csv")
            throw new ArgumentException("Export format must be csv or pdf.");

        return (
            Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(FiskalyStatisticsCsvBuilder.Build(stats))).ToArray(),
            "text/csv; charset=utf-8",
            $"fiskaly-statistics_{stamp}.csv");
    }

    internal static (DateTime FromUtc, DateTime ToUtc) ResolveRange(DateTime? fromUtc, DateTime? toUtc)
    {
        var to = (toUtc ?? DateTime.UtcNow).ToUniversalTime();
        var from = (fromUtc ?? to.AddDays(-DefaultLookbackDays)).ToUniversalTime();
        if (to <= from)
            throw new ArgumentException("toUtc must be after fromUtc.");
        if ((to - from).TotalDays > MaxRangeDays)
            throw new ArgumentException($"Date range cannot exceed {MaxRangeDays} days.");
        return (from, to);
    }

    internal static FiskalyStatisticsDto Build(
        DateTime fromUtc,
        DateTime toUtc,
        Guid? tenantId,
        IReadOnlyList<StatRow> rows)
    {
        var total = rows.Count;
        var success = rows.Count(r => string.Equals(r.Status, FiskalyOperationHistoryStatuses.Success, StringComparison.OrdinalIgnoreCase));
        var failed = rows.Count(r => string.Equals(r.Status, FiskalyOperationHistoryStatuses.Failed, StringComparison.OrdinalIgnoreCase));
        var inFlight = rows.Count(r => FiskalyOperationHistoryStatuses.IsInFlight(r.Status));
        var durations = rows
            .Where(r => r.CompletedAtUtc is { } completed && completed >= r.CreatedAtUtc)
            .Select(r => (r.CompletedAtUtc!.Value - r.CreatedAtUtc).TotalMilliseconds)
            .ToList();

        var byType = rows
            .GroupBy(r => string.IsNullOrWhiteSpace(r.OperationType) ? "unknown" : r.OperationType.Trim().ToLowerInvariant())
            .Select(g => new FiskalyStatisticsCountDto { Key = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .ToList();

        var byStatus = new[]
            {
                FiskalyOperationHistoryStatuses.Success,
                FiskalyOperationHistoryStatuses.Failed,
                FiskalyOperationHistoryStatuses.Pending,
                FiskalyOperationHistoryStatuses.Processing
            }
            .Select(status => new FiskalyStatisticsCountDto
            {
                Key = status,
                Count = rows.Count(r => string.Equals(r.Status, status, StringComparison.OrdinalIgnoreCase))
            })
            .ToList();

        return new FiskalyStatisticsDto
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            TenantId = tenantId is { } id && id != Guid.Empty ? id : null,
            Kpis = new FiskalyStatisticsKpisDto
            {
                TotalOperations = total,
                SuccessRatePercent = total == 0 ? 0 : Math.Round(100d * success / total, 2),
                MostUsedOperationType = byType.FirstOrDefault()?.Key,
                AverageProcessingTimeMs = durations.Count == 0 ? null : Math.Round(durations.Average(), 1),
                TotalErrors = failed,
                SuccessCount = success,
                FailedCount = failed,
                InFlightCount = inFlight
            },
            Daily = FillDaily(fromUtc, toUtc, rows),
            ByOperationType = byType,
            ByStatus = byStatus,
            Monthly = FillMonthly(fromUtc, toUtc, rows)
        };
    }

    private IQueryable<FiskalyOperationHistory> BaseQuery(bool actorIsSuperAdmin)
    {
        IQueryable<FiskalyOperationHistory> query = _db.FiskalyOperationHistories.AsNoTracking();
        if (actorIsSuperAdmin)
            query = query.IgnoreQueryFilters();
        return query;
    }

    private static IQueryable<FiskalyOperationHistory> ApplyFilters(
        IQueryable<FiskalyOperationHistory> query,
        FiskalyStatisticsQuery filter,
        bool actorIsSuperAdmin,
        DateTime fromUtc,
        DateTime toUtc)
    {
        query = query.Where(x => x.CreatedAtUtc >= fromUtc && x.CreatedAtUtc <= toUtc);

        if (actorIsSuperAdmin && filter.TenantId is { } tenantFilter && tenantFilter != Guid.Empty)
            query = query.Where(x => x.TenantId == tenantFilter);

        if (!string.IsNullOrWhiteSpace(filter.OperationType) && FiskalyOperationTypes.IsKnown(filter.OperationType))
        {
            var op = filter.OperationType.Trim().ToLowerInvariant();
            query = query.Where(x => x.OperationType == op);
        }

        return query;
    }

    private static List<FiskalyStatisticsDailyPointDto> FillDaily(
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlyList<StatRow> rows)
    {
        var grouped = rows
            .GroupBy(r => DateOnly.FromDateTime(r.CreatedAtUtc))
            .ToDictionary(g => g.Key, g => g.ToList());

        var list = new List<FiskalyStatisticsDailyPointDto>();
        for (var day = DateOnly.FromDateTime(fromUtc); day <= DateOnly.FromDateTime(toUtc); day = day.AddDays(1))
        {
            grouped.TryGetValue(day, out var bucket);
            list.Add(new FiskalyStatisticsDailyPointDto
            {
                Date = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Total = bucket?.Count ?? 0,
                Success = bucket?.Count(r => string.Equals(r.Status, FiskalyOperationHistoryStatuses.Success, StringComparison.OrdinalIgnoreCase)) ?? 0,
                Failed = bucket?.Count(r => string.Equals(r.Status, FiskalyOperationHistoryStatuses.Failed, StringComparison.OrdinalIgnoreCase)) ?? 0
            });
        }

        return list;
    }

    private static List<FiskalyStatisticsMonthlyPointDto> FillMonthly(
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlyList<StatRow> rows)
    {
        var grouped = rows
            .GroupBy(r => new YearMonth(r.CreatedAtUtc.Year, r.CreatedAtUtc.Month))
            .ToDictionary(g => g.Key, g => g.ToList());

        var list = new List<FiskalyStatisticsMonthlyPointDto>();
        var cursor = new DateTime(fromUtc.Year, fromUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(toUtc.Year, toUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        while (cursor <= end)
        {
            var key = new YearMonth(cursor.Year, cursor.Month);
            grouped.TryGetValue(key, out var bucket);
            list.Add(new FiskalyStatisticsMonthlyPointDto
            {
                YearMonth = cursor.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                Total = bucket?.Count ?? 0,
                Success = bucket?.Count(r => string.Equals(r.Status, FiskalyOperationHistoryStatuses.Success, StringComparison.OrdinalIgnoreCase)) ?? 0,
                Failed = bucket?.Count(r => string.Equals(r.Status, FiskalyOperationHistoryStatuses.Failed, StringComparison.OrdinalIgnoreCase)) ?? 0
            });
            cursor = cursor.AddMonths(1);
        }

        return list;
    }

    internal sealed record StatRow(
        DateTime CreatedAtUtc,
        DateTime? CompletedAtUtc,
        string Status,
        string OperationType);

    private readonly record struct YearMonth(int Year, int Month);
}
