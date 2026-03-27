using System.Globalization;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Time;

namespace KasseAPI_Final.Services;

/// <summary>
/// POS ödeme satırlarından operatör / muhasebe raporları (read-only).
/// </summary>
/// <remarks>
/// TODO (genişletme): Ödeme satırı «durum» filtresi (yalnızca Verkauf / nur Erstattung / inkl. Storno) için ayrı query parametresi;
/// şu an <c>activeOnly</c> ve satış/refund ayrımı toplamlarda yansır ancak tek endpoint parametresi yok.
/// </remarks>
public interface IOperationalReportingService
{
    Task<OperationalSummaryDto> GetSummaryAsync(
        DateTime? startDate,
        DateTime? endDate,
        Guid? cashRegisterId,
        string? cashierId,
        int? paymentMethod,
        bool activeOnly,
        CancellationToken cancellationToken = default);

    Task<PeriodicOperationalReportDto> GetPeriodicAsync(
        string periodPreset,
        DateTime? startDate,
        DateTime? endDate,
        Guid? cashRegisterId,
        string? cashierId,
        int? paymentMethod,
        bool activeOnly,
        CancellationToken cancellationToken = default);

    Task<InterimOperationalReportDto> GetInterimAsync(
        Guid? cashRegisterId,
        string? cashierId,
        int? paymentMethod,
        bool activeOnly,
        CancellationToken cancellationToken = default);

    Task<ClosingReferenceReportDto> GetClosingReferenceAsync(
        DateTime? startDate,
        DateTime? endDate,
        Guid? cashRegisterId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kasiyer-Leistung: nur <c>payment_details</c>, optional Tag×Kasiyer-Aufschlüsselung.
    /// </summary>
    Task<StaffPerformanceReportDto> GetStaffPerformanceAsync(
        DateTime? startDate,
        DateTime? endDate,
        Guid? cashRegisterId,
        string? cashierId,
        int? paymentMethod,
        bool activeOnly,
        bool includePerStaffPerDay,
        CancellationToken cancellationToken = default);
}

public sealed class OperationalReportingService : IOperationalReportingService
{
    private const decimal AnomalyRefundRowsPerSaleThreshold = 0.35m;
    private const decimal AnomalyStornoRowsPerSaleThreshold = 0.2m;

    private readonly AppDbContext _db;
    private readonly ILogger<OperationalReportingService> _logger;

    public OperationalReportingService(AppDbContext db, ILogger<OperationalReportingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<OperationalSummaryDto> GetSummaryAsync(
        DateTime? startDate,
        DateTime? endDate,
        Guid? cashRegisterId,
        string? cashierId,
        int? paymentMethod,
        bool activeOnly,
        CancellationToken cancellationToken = default)
    {
        var (fromUtc, endBoundUtc, endExclusive, repStart, repEnd) = ResolveRange(startDate, endDate);
        return await BuildSummaryInternalAsync(
            fromUtc,
            endBoundUtc,
            endExclusive,
            repStart,
            repEnd,
            "custom",
            cashRegisterId,
            cashierId,
            paymentMethod,
            activeOnly,
            interimDisclaimer: null,
            cancellationToken);
    }

    public async Task<PeriodicOperationalReportDto> GetPeriodicAsync(
        string periodPreset,
        DateTime? startDate,
        DateTime? endDate,
        Guid? cashRegisterId,
        string? cashierId,
        int? paymentMethod,
        bool activeOnly,
        CancellationToken cancellationToken = default)
    {
        var preset = (periodPreset ?? "custom").Trim().ToLowerInvariant();
        DateTime? s = startDate;
        DateTime? e = endDate;

        if (preset == "day")
        {
            var today = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
            s = e = today;
        }
        else if (preset == "week")
        {
            var today = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
            s = today.AddDays(-6);
            e = today;
        }
        else if (preset == "month")
        {
            var today = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
            s = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
            e = s.Value.AddMonths(1).AddDays(-1);
        }

        var (fromUtc, endBoundUtc, endExclusive, repStart, repEnd) = ResolveRange(s, e);
        var summary = await BuildSummaryInternalAsync(
            fromUtc,
            endBoundUtc,
            endExclusive,
            repStart,
            repEnd,
            preset,
            cashRegisterId,
            cashierId,
            paymentMethod,
            activeOnly,
            interimDisclaimer: null,
            cancellationToken);
        return new PeriodicOperationalReportDto { Summary = summary };
    }

    public async Task<InterimOperationalReportDto> GetInterimAsync(
        Guid? cashRegisterId,
        string? cashierId,
        int? paymentMethod,
        bool activeOnly,
        CancellationToken cancellationToken = default)
    {
        var today = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
        var (fromUtc, toExclusive) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(today);
        var nowUtc = DateTime.UtcNow;
        var disclaimer =
            "INTERIM (operator X): Totals since start of Austria business day in UTC window — not a hardware TSE X report; use for shift checks only.";

        var summary = await BuildSummaryInternalAsync(
            fromUtc,
            nowUtc,
            endExclusive: false,
            repStart: today,
            repEnd: today,
            periodPreset: "interim_today",
            cashRegisterId,
            cashierId,
            paymentMethod,
            activeOnly,
            interimDisclaimer: disclaimer,
            cancellationToken);
        return new InterimOperationalReportDto { Summary = summary };
    }

    public async Task<ClosingReferenceReportDto> GetClosingReferenceAsync(
        DateTime? startDate,
        DateTime? endDate,
        Guid? cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        var (fromUtc, endBoundUtc, endExclusive, repStart, repEnd) = ResolveRange(startDate, endDate);

        var q = _db.DailyClosings.AsNoTracking()
            .Where(c => c.ClosingType == "Daily");

        if (cashRegisterId.HasValue)
            q = q.Where(c => c.CashRegisterId == cashRegisterId.Value);

        if (endExclusive)
            q = q.Where(c => c.ClosingDate >= fromUtc && c.ClosingDate < endBoundUtc);
        else
            q = q.Where(c => c.ClosingDate >= fromUtc && c.ClosingDate <= endBoundUtc);

        var rows = await q
            .OrderByDescending(c => c.ClosingDate)
            .Take(500)
            .Select(c => new ClosingReferenceRowDto
            {
                Id = c.Id,
                CashRegisterId = c.CashRegisterId,
                UserId = c.UserId,
                ClosingDateUtc = c.ClosingDate,
                ClosingType = c.ClosingType,
                TotalAmount = c.TotalAmount,
                TotalTaxAmount = c.TotalTaxAmount,
                TransactionCount = c.TransactionCount,
                Status = c.Status,
                HasTseSignature = c.TseSignature != null && c.TseSignature.Length > 0,
            })
            .ToListAsync(cancellationToken);

        return new ClosingReferenceReportDto
        {
            Meta = new OperationalReportMetaDto
            {
                SchemaVersion = "1.0",
                ReportGeneratedAtUtc = DateTime.UtcNow,
                PeriodStartUtc = fromUtc,
                PeriodEndUtc = endBoundUtc,
                PeriodStartLocalDate = repStart,
                PeriodEndLocalDate = repEnd,
                PeriodPreset = "closings_daily",
                CashRegisterId = cashRegisterId,
            },
            DailyClosings = rows,
        };
    }

    /// <inheritdoc />
    public async Task<StaffPerformanceReportDto> GetStaffPerformanceAsync(
        DateTime? startDate,
        DateTime? endDate,
        Guid? cashRegisterId,
        string? cashierId,
        int? paymentMethod,
        bool activeOnly,
        bool includePerStaffPerDay,
        CancellationToken cancellationToken = default)
    {
        var (fromUtc, endBoundUtc, endExclusive, repStart, repEnd) = ResolveRange(startDate, endDate);

        IQueryable<PaymentDetails> q = _db.PaymentDetails.AsNoTracking();

        if (endExclusive)
            q = q.Where(p => p.CreatedAt >= fromUtc && p.CreatedAt < endBoundUtc);
        else
            q = q.Where(p => p.CreatedAt >= fromUtc && p.CreatedAt <= endBoundUtc);

        if (cashRegisterId.HasValue)
            q = q.Where(p => p.CashRegisterId == cashRegisterId.Value);

        if (!string.IsNullOrWhiteSpace(cashierId))
            q = q.Where(p => p.CashierId == cashierId);

        if (paymentMethod.HasValue)
            q = q.Where(p => p.PaymentMethodRaw == paymentMethod.Value.ToString(CultureInfo.InvariantCulture));

        if (activeOnly)
            q = q.Where(p => p.IsActive);

        var rawRows = await q
            .Select(p => new
            {
                p.CashierId,
                p.CreatedAt,
                p.IsRefund,
                p.IsStorno,
                p.TotalAmount,
                p.PaymentMethodRaw,
            })
            .ToListAsync(cancellationToken);

        var projected = rawRows
            .Select(p => new PaymentStaffProjection(
                p.CashierId,
                p.CreatedAt,
                p.IsRefund,
                p.IsStorno,
                p.TotalAmount,
                p.PaymentMethodRaw))
            .ToList();

        var cashierIds = projected.Select(p => p.CashierId).Distinct().ToList();
        var names = await ResolveCashierDisplayNamesAsync(cashierIds, cancellationToken);

        var byStaff = projected
            .GroupBy(p => p.CashierId)
            .Select(g => BuildStaffRow(g.Key, g.ToList(), names))
            .OrderByDescending(r => r.GrossSalesAmount)
            .ToList();

        var methodSlices = projected
            .Where(p => !p.IsRefund && !p.IsStorno)
            .GroupBy(p => (p.CashierId, p.PaymentMethodRaw))
            .Select(g => new StaffPerformanceStaffMethodSliceDto
            {
                CashierId = g.Key.CashierId,
                PaymentMethodRaw = g.Key.PaymentMethodRaw,
                SaleCount = g.Count(),
                GrossAmount = g.Sum(x => x.TotalAmount),
            })
            .OrderByDescending(x => x.GrossAmount)
            .ToList();

        var saleRows = projected.Where(p => !p.IsRefund && !p.IsStorno).ToList();
        var refundRows = projected.Where(p => p.IsRefund).ToList();
        var stornoRows = projected.Where(p => p.IsStorno).ToList();

        var aggregateByDay = projected
            .GroupBy(p => ViennaDayKey(p.CreatedAt))
            .Select(g => new StaffPerformanceLocalDayAggregateDto
            {
                LocalDayYyyyMmDd = g.Key,
                SaleTransactionCount = g.Count(p => !p.IsRefund && !p.IsStorno),
                GrossSalesAmount = g.Where(p => !p.IsRefund && !p.IsStorno).Sum(p => p.TotalAmount),
                RefundRowCount = g.Count(p => p.IsRefund),
                StornoRowCount = g.Count(p => p.IsStorno),
            })
            .OrderBy(x => x.LocalDayYyyyMmDd)
            .ToList();

        List<StaffPerformanceLocalDayStaffDto> perDayStaff = new();
        if (includePerStaffPerDay)
        {
            perDayStaff = projected
                .GroupBy(p => (Day: ViennaDayKey(p.CreatedAt), p.CashierId))
                .Select(g => new StaffPerformanceLocalDayStaffDto
                {
                    LocalDayYyyyMmDd = g.Key.Day,
                    CashierId = g.Key.CashierId,
                    SaleTransactionCount = g.Count(p => !p.IsRefund && !p.IsStorno),
                    GrossSalesAmount = g.Where(p => !p.IsRefund && !p.IsStorno).Sum(p => p.TotalAmount),
                    RefundRowCount = g.Count(p => p.IsRefund),
                    StornoRowCount = g.Count(p => p.IsStorno),
                })
                .OrderBy(x => x.LocalDayYyyyMmDd)
                .ThenBy(x => x.CashierId)
                .ToList();
        }

        var anomalies = new List<StaffPerformanceAnomalyDto>();
        foreach (var row in byStaff)
        {
            if (row.SaleTransactionCount == 0)
                continue;
            if (row.RefundRowsPerSale >= AnomalyRefundRowsPerSaleThreshold)
            {
                anomalies.Add(new StaffPerformanceAnomalyDto
                {
                    Kind = "ELEVATED_REFUND_FREQUENCY",
                    Severity = "warning",
                    CashierId = row.CashierId,
                    Message =
                        $"Refund count ({row.RefundRowCount}) relative to sale count ({row.SaleTransactionCount}) exceeds threshold {AnomalyRefundRowsPerSaleThreshold:P0}.",
                    MetricValue = row.RefundRowsPerSale,
                    Threshold = AnomalyRefundRowsPerSaleThreshold,
                });
            }

            if (row.StornoRowsPerSale >= AnomalyStornoRowsPerSaleThreshold)
            {
                anomalies.Add(new StaffPerformanceAnomalyDto
                {
                    Kind = "ELEVATED_STORNO_FREQUENCY",
                    Severity = "warning",
                    CashierId = row.CashierId,
                    Message =
                        $"Storno count ({row.StornoRowCount}) relative to sale count ({row.SaleTransactionCount}) exceeds threshold {AnomalyStornoRowsPerSaleThreshold:P0}.",
                    MetricValue = row.StornoRowsPerSale,
                    Threshold = AnomalyStornoRowsPerSaleThreshold,
                });
            }
        }

        return new StaffPerformanceReportDto
        {
            Meta = new OperationalReportMetaDto
            {
                SchemaVersion = "1.1-staff",
                ReportGeneratedAtUtc = DateTime.UtcNow,
                PeriodStartUtc = fromUtc,
                PeriodEndUtc = endBoundUtc,
                PeriodStartLocalDate = repStart,
                PeriodEndLocalDate = repEnd,
                PeriodPreset = "staff_performance",
                CashRegisterId = cashRegisterId,
                CashierId = cashierId,
                PaymentMethodFilter = paymentMethod,
                ActiveOnly = activeOnly,
            },
            Reliability = new StaffPerformanceReliabilityDto(),
            Totals = new StaffPerformanceTotalsDto
            {
                SaleTransactionCount = saleRows.Count,
                GrossSalesAmount = saleRows.Sum(p => p.TotalAmount),
                RefundRowCount = refundRows.Count,
                RefundAmountTotal = refundRows.Sum(p => p.TotalAmount),
                StornoRowCount = stornoRows.Count,
            },
            ByStaff = byStaff,
            ByStaffAndPaymentMethod = methodSlices,
            AggregateByLocalDay = aggregateByDay,
            ByLocalDayAndStaff = perDayStaff,
            Anomalies = anomalies,
        };
    }

    private sealed record PaymentStaffProjection(
        string CashierId,
        DateTime CreatedAt,
        bool IsRefund,
        bool IsStorno,
        decimal TotalAmount,
        string PaymentMethodRaw);

    private static string ViennaDayKey(DateTime createdAtUtc)
    {
        var utc = PostgreSqlUtcDateTime.InstantToPersistUtc(createdAtUtc);
        return PostgreSqlUtcDateTime.FormatViennaUtcInstantAsYyyyMmDd(utc);
    }

    private async Task<Dictionary<string, (string? UserName, string? Email)>> ResolveCashierDisplayNamesAsync(
        IReadOnlyCollection<string> cashierIds,
        CancellationToken cancellationToken)
    {
        if (cashierIds.Count == 0)
            return new Dictionary<string, (string?, string?)>(StringComparer.Ordinal);

        var rows = await _db.Users.AsNoTracking()
            .Where(u => cashierIds.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName, u.Email })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(x => x.Id, x => (x.UserName, x.Email), StringComparer.Ordinal);
    }

    private static StaffPerformanceStaffRowDto BuildStaffRow(
        string cashierId,
        List<PaymentStaffProjection> rows,
        IReadOnlyDictionary<string, (string? UserName, string? Email)> names)
    {
        var sales = rows.Where(p => !p.IsRefund && !p.IsStorno).ToList();
        var refunds = rows.Where(p => p.IsRefund).ToList();
        var stornos = rows.Where(p => p.IsStorno).ToList();

        var saleCount = sales.Count;
        var gross = sales.Sum(p => p.TotalAmount);
        var refundAmt = refunds.Sum(p => p.TotalAmount);

        names.TryGetValue(cashierId, out var name);

        decimal refundRowsPerSale = saleCount == 0 ? 0 : (decimal)refunds.Count / saleCount;
        decimal stornoRowsPerSale = saleCount == 0 ? 0 : (decimal)stornos.Count / saleCount;
        decimal refundAmountRatio = gross == 0 ? 0 : Math.Abs(refundAmt) / gross;

        return new StaffPerformanceStaffRowDto
        {
            CashierId = cashierId,
            UserName = name.UserName,
            Email = name.Email,
            SaleTransactionCount = saleCount,
            GrossSalesAmount = gross,
            RefundRowCount = refunds.Count,
            RefundAmountTotal = refundAmt,
            StornoRowCount = stornos.Count,
            RefundRowsPerSale = refundRowsPerSale,
            StornoRowsPerSale = stornoRowsPerSale,
            RefundAmountToGrossRatio = refundAmountRatio,
        };
    }

    private async Task<OperationalSummaryDto> BuildSummaryInternalAsync(
        DateTime fromUtc,
        DateTime endBoundUtc,
        bool endExclusive,
        DateTime repStart,
        DateTime repEnd,
        string periodPreset,
        Guid? cashRegisterId,
        string? cashierId,
        int? paymentMethod,
        bool activeOnly,
        string? interimDisclaimer,
        CancellationToken cancellationToken)
    {
        IQueryable<PaymentDetails> q = _db.PaymentDetails.AsNoTracking();

        if (endExclusive)
            q = q.Where(p => p.CreatedAt >= fromUtc && p.CreatedAt < endBoundUtc);
        else
            q = q.Where(p => p.CreatedAt >= fromUtc && p.CreatedAt <= endBoundUtc);

        if (cashRegisterId.HasValue)
            q = q.Where(p => p.CashRegisterId == cashRegisterId.Value);

        if (!string.IsNullOrWhiteSpace(cashierId))
            q = q.Where(p => p.CashierId == cashierId);

        if (paymentMethod.HasValue)
            q = q.Where(p => p.PaymentMethodRaw == paymentMethod.Value.ToString(CultureInfo.InvariantCulture));

        if (activeOnly)
            q = q.Where(p => p.IsActive);

        var list = await q.ToListAsync(cancellationToken);

        var saleLike = list.Where(p => !p.IsRefund && !p.IsStorno).ToList();
        var refunds = list.Where(p => p.IsRefund).ToList();

        var gross = saleLike.Sum(p => p.TotalAmount);
        var tax = saleLike.Sum(p => p.TaxAmount);
        var refundTotal = refunds.Sum(p => p.TotalAmount);

        var byMethod = saleLike
            .GroupBy(p => p.PaymentMethodRaw)
            .Select(g => new PaymentMethodBucketDto
            {
                MethodKey = g.Key,
                Count = g.Count(),
                TotalAmount = g.Sum(x => x.TotalAmount),
            })
            .OrderByDescending(x => x.TotalAmount)
            .ToList();

        var byCashier = saleLike
            .GroupBy(p => p.CashierId)
            .Select(g => new CashierBucketDto
            {
                CashierId = g.Key,
                Count = g.Count(),
                TotalAmount = g.Sum(x => x.TotalAmount),
            })
            .OrderByDescending(x => x.TotalAmount)
            .ToList();

        return new OperationalSummaryDto
        {
            Meta = new OperationalReportMetaDto
            {
                SchemaVersion = "1.0",
                ReportGeneratedAtUtc = DateTime.UtcNow,
                PeriodStartUtc = fromUtc,
                PeriodEndUtc = endBoundUtc,
                PeriodStartLocalDate = repStart,
                PeriodEndLocalDate = repEnd,
                PeriodPreset = periodPreset,
                CashRegisterId = cashRegisterId,
                CashierId = cashierId,
                PaymentMethodFilter = paymentMethod,
                ActiveOnly = activeOnly,
            },
            PaymentRowCount = list.Count,
            GrossTotalAmount = gross,
            TaxTotalAmount = tax,
            RefundRowCount = refunds.Count,
            RefundAmountTotal = refundTotal,
            ByPaymentMethod = byMethod,
            ByCashier = byCashier,
            InterimDisclaimer = interimDisclaimer,
            ClosingDisclaimer = null,
        };
    }

    /// <summary>
    /// ReportsController ile aynı takvim/rolling semantiği (fatura raporlarıyla hizalı).
    /// </summary>
    private static (DateTime FromUtc, DateTime EndBoundUtc, bool EndExclusive, DateTime RepStart, DateTime RepEnd) ResolveRange(
        DateTime? startDate,
        DateTime? endDate)
    {
        var nowUtc = DateTime.UtcNow;
        if (!startDate.HasValue && !endDate.HasValue)
        {
            var fromUtc = nowUtc.AddDays(-30);
            return (fromUtc, nowUtc, false, fromUtc, nowUtc);
        }

        var s = startDate ?? endDate!.Value;
        var e = endDate ?? startDate!.Value;
        var reportStart = new DateTime(s.Year, s.Month, s.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var reportEnd = new DateTime(e.Year, e.Month, e.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var (fromUtcCal, toExclusiveUtc) = PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(s, e);
        return (fromUtcCal, toExclusiveUtc, true, reportStart, reportEnd);
    }
}
