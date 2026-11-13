using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Reports;

public interface IPeakHoursAnalysisService
{
    Task<PeakHoursReportDto> GetPeakHoursAsync(
        DateTime? startDate,
        DateTime? endDate,
        Guid? cashRegisterId,
        CancellationToken cancellationToken = default);
}

public sealed class PeakHoursAnalysisService : IPeakHoursAnalysisService
{
    private readonly AppDbContext _db;

    public PeakHoursAnalysisService(AppDbContext db) => _db = db;

    public async Task<PeakHoursReportDto> GetPeakHoursAsync(
        DateTime? startDate,
        DateTime? endDate,
        Guid? cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        var (fromUtc, endBoundUtc, endExclusive, repStart, repEnd) = AdminReportQueryRange.Resolve(startDate, endDate);
        var registerIds = await ResolveRegisterIdsAsync(cashRegisterId, cancellationToken);

        var q = _db.PaymentDetails.AsNoTracking()
            .Where(p => registerIds.Contains(p.CashRegisterId) && p.IsActive && !p.IsStorno && p.RksvSpecialReceiptKind == null);
        q = endExclusive
            ? q.Where(p => p.CreatedAt >= fromUtc && p.CreatedAt < endBoundUtc)
            : q.Where(p => p.CreatedAt >= fromUtc && p.CreatedAt <= endBoundUtc);

        var payments = await q.Select(p => p.CreatedAt).ToListAsync(cancellationToken);

        var heatmap = new int[7][];
        for (var d = 0; d < 7; d++)
            heatmap[d] = new int[24];

        foreach (var createdAt in payments)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(createdAt, PostgreSqlUtcDateTime.AustriaTimeZone);
            var dow = ((int)local.DayOfWeek + 6) % 7;
            heatmap[dow][local.Hour]++;
        }

        PeakHourSlotDto? busiest = null;
        PeakHourSlotDto? quietest = null;
        for (var d = 0; d < 7; d++)
        {
            for (var h = 0; h < 24; h++)
            {
                var c = heatmap[d][h];
                var slot = new PeakHourSlotDto { Day = d, Hour = h, TransactionCount = c };
                if (busiest == null || c > busiest.TransactionCount)
                    busiest = slot;
                if (quietest == null || c < quietest.TransactionCount)
                    quietest = slot;
            }
        }

        var periodDays = Math.Max((repEnd - repStart).TotalDays + 1, 1);
        var totalHoursInPeriod = periodDays * 24;
        var averagePerHour = totalHoursInPeriod > 0 ? payments.Count / totalHoursInPeriod : 0;

        var hourlyTotals = new int[24];
        for (var d = 0; d < 7; d++)
            for (var h = 0; h < 24; h++)
                hourlyTotals[h] += heatmap[d][h];

        var nonZero = hourlyTotals.Where(x => x > 0).ToList();
        var p75 = nonZero.Count > 0
            ? nonZero.OrderBy(x => x).ElementAt(Math.Min(nonZero.Count - 1, (int)(nonZero.Count * 0.75)))
            : 0;

        var staffing = Enumerable.Range(0, 24)
            .Select(h => new StaffingRecommendationDto
            {
                Hour = h,
                SuggestedStaff = hourlyTotals[h] >= Math.Max(p75, 1) ? 2 : 1,
            })
            .ToList();

        return new PeakHoursReportDto
        {
            CashRegisterId = cashRegisterId,
            PeriodStartLocal = repStart,
            PeriodEndLocal = repEnd,
            Heatmap = heatmap,
            MaxCellCount = heatmap.SelectMany(c => c).DefaultIfEmpty(0).Max(),
            BusiestHour = busiest,
            QuietestHour = quietest,
            AverageTransactionsPerHour = averagePerHour,
            RecommendedStaffingLevels = staffing,
        };
    }

    private async Task<List<Guid>> ResolveRegisterIdsAsync(Guid? cashRegisterId, CancellationToken cancellationToken)
    {
        if (cashRegisterId.HasValue && cashRegisterId.Value != Guid.Empty)
            return [cashRegisterId.Value];

        return await _db.CashRegisters.AsNoTracking().Select(r => r.Id).ToListAsync(cancellationToken);
    }
}
