using System.Globalization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public interface IDepExportStatisticsService
{
    Task<DepExportStatisticsDto> GetStatisticsAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DepExportTrendPointDto>> GetTrendAsync(
        Guid tenantId,
        int months = 12,
        CancellationToken cancellationToken = default);

    Task<DepExportForecastDto> GetForecastAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Operational statistics over <see cref="DepExportHistory"/> (manual + scheduled).
/// Not an official BMF/RKSV certification metric.
/// </summary>
public sealed class DepExportStatisticsService : IDepExportStatisticsService
{
    private readonly AppDbContext _db;
    private readonly IDepExportRequirementService _requirementService;
    private readonly TimeProvider _time;

    public DepExportStatisticsService(
        AppDbContext db,
        IDepExportRequirementService requirementService,
        TimeProvider time)
    {
        _db = db;
        _requirementService = requirementService;
        _time = time;
    }

    public async Task<DepExportStatisticsDto> GetStatisticsAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        fromUtc = EnsureUtc(fromUtc);
        toUtc = EnsureUtc(toUtc);
        if (toUtc < fromUtc)
            (fromUtc, toUtc) = (toUtc, fromUtc);

        var rows = await _db.DepExportHistories
            .AsNoTracking()
            .Where(h =>
                h.TenantId == tenantId &&
                h.ExportedAt >= fromUtc &&
                h.ExportedAt <= toUtc)
            .Select(h => new
            {
                h.Status,
                h.ScheduleId,
                h.ExportedAt,
                h.FileSizeBytes,
                h.FromUtc,
                h.ToUtc,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var successful = rows.Count(r => r.Status == DepExportStatus.Completed.ToString());
        var failed = rows.Count(r => r.Status == DepExportStatus.Failed.ToString());
        var terminal = successful + failed;
        var completedSizes = rows
            .Where(r => r.Status == DepExportStatus.Completed.ToString())
            .Select(r => r.FileSizeBytes)
            .ToList();

        var byType = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Manual"] = rows.Count(r => r.ScheduleId is null),
            ["Scheduled"] = rows.Count(r => r.ScheduleId is not null),
        };

        foreach (var row in rows.Where(r => r.Status == DepExportStatus.Completed.ToString()))
        {
            var periodType = ClassifyPeriodType(row.FromUtc, row.ToUtc);
            byType[periodType] = byType.TryGetValue(periodType, out var n) ? n + 1 : 1;
        }

        var byYear = rows
            .GroupBy(r => r.ExportedAt.ToUniversalTime().Year.ToString(CultureInfo.InvariantCulture))
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Count());

        var next = await _requirementService
            .GetNextRequirementAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);

        return new DepExportStatisticsDto
        {
            TotalExports = rows.Count,
            SuccessfulExports = successful,
            FailedExports = failed,
            SuccessRate = terminal <= 0 ? 0 : Math.Round(100.0 * successful / terminal, 2),
            ExportsByType = byType,
            ExportsByYear = byYear,
            AverageExportSizeBytes = completedSizes.Count == 0 ? 0 : completedSizes.Average(),
            TotalStorageUsedMb = BytesToMb(completedSizes.Sum()),
            LastExportDate = rows.Count == 0 ? null : rows.Max(r => r.ExportedAt),
            NextDueDate = next?.DueDate,
            FromUtc = fromUtc,
            ToUtc = toUtc,
        };
    }

    public async Task<IReadOnlyList<DepExportTrendPointDto>> GetTrendAsync(
        Guid tenantId,
        int months = 12,
        CancellationToken cancellationToken = default)
    {
        months = Math.Clamp(months, 1, 36);
        var now = _time.GetUtcNow().UtcDateTime;
        var startMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-(months - 1));
        var endExclusive = startMonth.AddMonths(months);

        var rows = await _db.DepExportHistories
            .AsNoTracking()
            .Where(h =>
                h.TenantId == tenantId &&
                h.ExportedAt >= startMonth &&
                h.ExportedAt < endExclusive)
            .Select(h => new { h.ExportedAt, h.Status, h.FileSizeBytes })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var points = new List<DepExportTrendPointDto>(months);
        for (var i = 0; i < months; i++)
        {
            var periodStart = startMonth.AddMonths(i);
            var periodEnd = periodStart.AddMonths(1);
            var monthRows = rows
                .Where(r => r.ExportedAt >= periodStart && r.ExportedAt < periodEnd)
                .ToList();

            points.Add(new DepExportTrendPointDto
            {
                PeriodStartUtc = periodStart,
                Label = periodStart.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                TotalExports = monthRows.Count,
                SuccessfulExports = monthRows.Count(r => r.Status == DepExportStatus.Completed.ToString()),
                FailedExports = monthRows.Count(r => r.Status == DepExportStatus.Failed.ToString()),
                TotalSizeBytes = monthRows
                    .Where(r => r.Status == DepExportStatus.Completed.ToString())
                    .Sum(r => r.FileSizeBytes),
            });
        }

        return points;
    }

    public async Task<DepExportForecastDto> GetForecastAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var trend = await GetTrendAsync(tenantId, months: 12, cancellationToken).ConfigureAwait(false);
        var avg = trend.Count == 0
            ? 0
            : trend.Average(p => (double)p.SuccessfulExports);

        var next = await _requirementService
            .GetNextRequirementAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);

        var now = _time.GetUtcNow().UtcDateTime;
        var nextMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
        var points = new List<DepExportForecastPointDto>(3);

        for (var i = 0; i < 3; i++)
        {
            var periodStart = nextMonth.AddMonths(i);
            var periodEnd = periodStart.AddMonths(1);
            var dueInMonth = next?.DueDate is DateTime due &&
                             due >= periodStart &&
                             due < periodEnd;

            // Slight uplift when a known due date falls in the month (expect at least one legal/recommended export).
            var projected = avg;
            if (dueInMonth)
                projected = Math.Max(projected, Math.Max(1, avg));

            points.Add(new DepExportForecastPointDto
            {
                PeriodStartUtc = periodStart,
                Label = periodStart.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                ProjectedExports = Math.Round(projected, 2),
                HasKnownDueDate = dueInMonth,
            });
        }

        return new DepExportForecastDto
        {
            GeneratedAtUtc = now,
            NextDueDate = next?.DueDate,
            NextRequirementTitle = next?.Title,
            AverageMonthlyExports = Math.Round(avg, 2),
            Points = points,
        };
    }

    /// <summary>Heuristic period label from export window length (completed exports only).</summary>
    internal static string ClassifyPeriodType(DateTime fromUtc, DateTime toUtc)
    {
        var days = Math.Abs((toUtc - fromUtc).TotalDays);
        if (days >= 300) return "YearlyWindow";
        if (days >= 80) return "QuarterlyWindow";
        if (days >= 20) return "MonthlyWindow";
        return "CustomWindow";
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

    private static double BytesToMb(long bytes) =>
        bytes <= 0 ? 0 : Math.Round(bytes / (1024.0 * 1024.0), 2);
}
