using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

    Task<PeriodenberichtRunDto> FreezePeriodicAsync(
        FreezePeriodenberichtRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PeriodenberichtRunListItemDto>> ListFrozenPeriodenberichteAsync(
        DateTime? fromDate,
        DateTime? toDate,
        Guid? cashRegisterId,
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task<PeriodenberichtRunDto?> GetFrozenPeriodenberichtByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// X/Z reference bundle: interim (X-like), full-day operational totals, and daily closing rows (Z-like).
    /// Deterministic read model — not persisted; see DTO disclaimers.
    /// </summary>
    Task<XzReferenceBundleDto> GetXzReferenceBundleAsync(
        DateTime? businessDate,
        Guid? cashRegisterId,
        string? cashierId,
        int? paymentMethod,
        bool activeOnly,
        CancellationToken cancellationToken = default);
}

public sealed class OperationalReportingService : IOperationalReportingService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private const decimal AnomalyRefundRowsPerSaleThreshold = 0.35m;
    private const decimal AnomalyStornoRowsPerSaleThreshold = 0.2m;

    private readonly AppDbContext _db;
    private readonly ILogger<OperationalReportingService> _logger;
    private readonly IAuditLogService _audit;

    public OperationalReportingService(AppDbContext db, ILogger<OperationalReportingService> logger, IAuditLogService audit)
    {
        _db = db;
        _logger = logger;
        _audit = audit;
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

        // RKSV Monats-Nullbeleg: zero signed receipt — exclude from operator / Umsatz aggregates.
        q = q.Where(p => p.RksvSpecialReceiptKind == null);

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

    public async Task<PeriodenberichtRunDto> FreezePeriodicAsync(
        FreezePeriodenberichtRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
            throw new InvalidOperationException("Actor required.");

        var periodic = await GetPeriodicAsync(
            request.PeriodPreset,
            request.StartDate,
            request.EndDate,
            request.CashRegisterId,
            request.CashierId,
            request.PaymentMethod,
            request.ActiveOnly,
            cancellationToken);

        var summary = periodic.Summary;
        var now = DateTime.UtcNow;
        var scopeKind = request.CashRegisterId.HasValue ? "Register" : "Company";
        var warnings = new List<string>();
        if (!string.IsNullOrWhiteSpace(summary.InterimDisclaimer)) warnings.Add(summary.InterimDisclaimer);
        if (!string.IsNullOrWhiteSpace(summary.ClosingDisclaimer)) warnings.Add(summary.ClosingDisclaimer);

        var queryParams = new
        {
            periodPreset = request.PeriodPreset ?? "custom",
            startDate = request.StartDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            endDate = request.EndDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            cashRegisterId = request.CashRegisterId,
            cashierId = request.CashierId,
            paymentMethod = request.PaymentMethod,
            activeOnly = request.ActiveOnly,
            scopeKind
        };

        var queryJson = JsonSerializer.Serialize(queryParams, JsonOpts);
        var snapshotJson = JsonSerializer.Serialize(summary, JsonOpts);

        var run = new PeriodenberichtRun
        {
            PeriodPreset = request.PeriodPreset ?? "custom",
            PeriodStartLocalDate = summary.Meta.PeriodStartLocalDate,
            PeriodEndLocalDate = summary.Meta.PeriodEndLocalDate,
            PeriodStartUtc = summary.Meta.PeriodStartUtc,
            PeriodEndUtc = summary.Meta.PeriodEndUtc,
            ScopeKind = scopeKind,
            CashRegisterId = request.CashRegisterId,
            CashierId = request.CashierId,
            PaymentMethodFilter = request.PaymentMethod,
            ActiveOnly = request.ActiveOnly,
            QueryParametersJson = queryJson,
            QueryParametersHash = ComputeSha256Hex(queryJson),
            SnapshotJson = snapshotJson,
            SnapshotHash = ComputeSha256Hex(snapshotJson),
            SnapshotSchemaVersion = summary.Meta.SchemaVersion ?? "1.0",
            PaymentRowCount = summary.PaymentRowCount,
            GrossTotalAmount = summary.GrossTotalAmount,
            TaxTotalAmount = summary.TaxTotalAmount,
            RefundRowCount = summary.RefundRowCount,
            RefundAmountTotal = summary.RefundAmountTotal,
            WarningsJson = JsonSerializer.Serialize(warnings, JsonOpts),
            GeneratedAtUtc = summary.Meta.ReportGeneratedAtUtc == default ? now : summary.Meta.ReportGeneratedAtUtc,
            CreatedAtUtc = now,
            CreatedByUserId = actorUserId,
            ExportProfileKey = request.ExportProfileKey,
            CorrelationId = request.CorrelationId
        };

        _db.PeriodenberichtRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogSystemOperationAsync(
            "PeriodenberichtFrozen",
            nameof(PeriodenberichtRun),
            actorUserId,
            "ReportActor",
            description: request.Note ?? "Periodenbericht frozen from operational periodic summary",
            requestData: new
            {
                reportType = "Periodenbericht",
                periodPreset = run.PeriodPreset,
                run.PeriodStartLocalDate,
                run.PeriodEndLocalDate,
                run.ScopeKind,
                run.CashRegisterId,
                run.CashierId,
                run.PaymentMethodFilter,
                run.ActiveOnly,
                run.QueryParametersHash,
                run.ExportProfileKey
            },
            responseData: new
            {
                runId = run.Id,
                run.SnapshotHash,
                run.SnapshotSchemaVersion,
                run.PaymentRowCount,
                run.GrossTotalAmount,
                run.RefundAmountTotal
            },
            correlationIdOverride: run.CorrelationId);

        return await MapFrozenRunAsync(run, cancellationToken);
    }

    public async Task<IReadOnlyList<PeriodenberichtRunListItemDto>> ListFrozenPeriodenberichteAsync(
        DateTime? fromDate,
        DateTime? toDate,
        Guid? cashRegisterId,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var q = _db.PeriodenberichtRuns.AsNoTracking().AsQueryable();

        if (fromDate.HasValue)
        {
            var lo = new DateTime(fromDate.Value.Year, fromDate.Value.Month, fromDate.Value.Day, 0, 0, 0, DateTimeKind.Unspecified);
            q = q.Where(x => x.PeriodStartLocalDate >= lo);
        }

        if (toDate.HasValue)
        {
            var hi = new DateTime(toDate.Value.Year, toDate.Value.Month, toDate.Value.Day, 0, 0, 0, DateTimeKind.Unspecified);
            q = q.Where(x => x.PeriodEndLocalDate <= hi);
        }

        if (cashRegisterId.HasValue)
            q = q.Where(x => x.CashRegisterId == cashRegisterId.Value);

        var rows = await q
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(Math.Max(1, Math.Min(limit, 500)))
            .Select(x => new PeriodenberichtRunListItemDto
            {
                Id = x.Id,
                CreatedAtUtc = x.CreatedAtUtc,
                GeneratedAtUtc = x.GeneratedAtUtc,
                PeriodPreset = x.PeriodPreset,
                PeriodStartLocalDate = x.PeriodStartLocalDate,
                PeriodEndLocalDate = x.PeriodEndLocalDate,
                ScopeKind = x.ScopeKind,
                CashRegisterId = x.CashRegisterId,
                CashierId = x.CashierId,
                PaymentMethodFilter = x.PaymentMethodFilter,
                ActiveOnly = x.ActiveOnly,
                SnapshotSchemaVersion = x.SnapshotSchemaVersion,
                PaymentRowCount = x.PaymentRowCount,
                GrossTotalAmount = x.GrossTotalAmount,
                TaxTotalAmount = x.TaxTotalAmount,
                RefundAmountTotal = x.RefundAmountTotal,
                QueryParametersHash = x.QueryParametersHash,
                SnapshotHash = x.SnapshotHash,
                CreatedByUserId = x.CreatedByUserId,
                ExportProfileKey = x.ExportProfileKey
            })
            .ToListAsync(cancellationToken);

        return rows;
    }

    public async Task<PeriodenberichtRunDto?> GetFrozenPeriodenberichtByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var run = await _db.PeriodenberichtRuns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (run == null) return null;
        return await MapFrozenRunAsync(run, cancellationToken);
    }

    public async Task<XzReferenceBundleDto> GetXzReferenceBundleAsync(
        DateTime? businessDate,
        Guid? cashRegisterId,
        string? cashierId,
        int? paymentMethod,
        bool activeOnly,
        CancellationToken cancellationToken = default)
    {
        var viennaDay = businessDate.HasValue
            ? new DateTime(businessDate.Value.Year, businessDate.Value.Month, businessDate.Value.Day, 0, 0, 0, DateTimeKind.Unspecified)
            : PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();

        var todayVienna = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
        var isToday = viennaDay.Date == todayVienna.Date;

        InterimOperationalReportDto? interim = null;
        if (isToday)
            interim = await GetInterimAsync(cashRegisterId, cashierId, paymentMethod, activeOnly, cancellationToken);

        var fullDay = await GetSummaryAsync(viennaDay, viennaDay, cashRegisterId, cashierId, paymentMethod, activeOnly, cancellationToken);
        var closing = await GetClosingReferenceAsync(viennaDay, viennaDay, cashRegisterId, cancellationToken);

        var closingRows = closing.DailyClosings ?? Array.Empty<ClosingReferenceRowDto>();
        var warnings = new List<string>();

        if (closingRows.Count == 0)
        {
            warnings.Add(
                "No Daily closing row found for this filter and business day (database daily_closing).");
        }
        else if (closingRows.Count > 1)
        {
            warnings.Add(
                $"Multiple Daily closings ({closingRows.Count}) in this window; review each row (retries, corrections, or unusual operations).");
        }

        XzInterimVsFullDayDto? interimVs = null;
        if (interim?.Summary != null)
        {
            var g1 = interim.Summary.GrossTotalAmount;
            var g2 = fullDay.GrossTotalAmount;
            var d = g1 - g2;
            interimVs = new XzInterimVsFullDayDto
            {
                InterimGrossTotal = g1,
                FullDayGrossTotal = g2,
                DeltaGross = d
            };
            if (Math.Abs(d) > 0.01m)
            {
                warnings.Add(
                    $"Interim vs full-day operational gross differs by {d:0.00} EUR (unexpected on the same business day; check filters and clock boundaries).");
            }
        }

        XzOperationalVsClosingDto? opVsClosing = null;
        if (closingRows.Count >= 1)
        {
            var primary = closingRows.OrderByDescending(x => x.ClosingDateUtc).First();
            var opGross = fullDay.GrossTotalAmount;
            var cTot = primary.TotalAmount;
            var delta = opGross - cTot;
            opVsClosing = new XzOperationalVsClosingDto
            {
                PrimaryClosingId = primary.Id,
                OperationalGrossTotal = opGross,
                ClosingTotalAmount = cTot,
                DeltaGross = delta
            };
            if (Math.Abs(delta) > 0.50m)
            {
                warnings.Add(
                    $"Operational gross and primary closing total differ by {delta:0.00} EUR; reconcile payment filters, refunds/storno, and TSE closing totals.");
            }
        }
        else if (interim?.Summary != null)
        {
            warnings.Add("Interim snapshot exists but no closing row yet — Z reference will populate after Tagesabschluss.");
        }

        var legal = new List<string>
        {
            "X/Z Reference Bundle — software read model combining operational aggregates (payment_details) and database daily closings. Not a hardware TSE-native X/Z printout unless produced by certified peripheral software.",
            closing.OperatorNote
        };
        if (!string.IsNullOrWhiteSpace(interim?.Summary?.InterimDisclaimer))
            legal.Add(interim.Summary.InterimDisclaimer!);

        var parts = new List<XzReferenceBundlePartDto>
        {
            new()
            {
                Kind = "full_day_operational",
                Label = "Full-day operational (payment_details)",
                Description = "Austria business calendar day window for the selected filters; not a hardware TSE X/Z."
            },
            new()
            {
                Kind = "closing_z_reference",
                Label = "Daily closing Z reference (database)",
                Description = "Rows from daily_closing (Tagesabschluss); not a substitute for hardware receipt unless your deployment prints it."
            }
        };
        if (interim != null)
        {
            parts.Insert(0, new XzReferenceBundlePartDto
            {
                Kind = "interim_x_like",
                Label = "Interim X-like (operator snapshot)",
                Description = "From start of Austria business day until now; not a hardware TSE X report."
            });
        }

        return new XzReferenceBundleDto
        {
            SchemaVersion = "1.0",
            GeneratedAtUtc = DateTime.UtcNow,
            ViennaBusinessDate = viennaDay,
            ScopeKind = cashRegisterId.HasValue ? "Register" : "Company",
            CashRegisterId = cashRegisterId,
            CashierId = cashierId,
            PaymentMethodFilter = paymentMethod,
            ActiveOnly = activeOnly,
            IsCurrentBusinessDay = isToday,
            LegalDisclaimers = legal,
            InterimXLike = interim,
            FullDayOperationalSummary = fullDay,
            ClosingReference = closing,
            LinkedClosingIds = closingRows.Select(r => r.Id).ToList(),
            Parts = parts,
            InformationalWarnings = warnings,
            InterimVsFullDaySnapshot = interimVs,
            OperationalVsClosing = opVsClosing
        };
    }

    private Task<PeriodenberichtRunDto> MapFrozenRunAsync(PeriodenberichtRun run, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var summary = JsonSerializer.Deserialize<OperationalSummaryDto>(run.SnapshotJson, JsonOpts) ?? new OperationalSummaryDto();
        var warnings = JsonSerializer.Deserialize<List<string>>(run.WarningsJson, JsonOpts) ?? new List<string>();

        return Task.FromResult(new PeriodenberichtRunDto
        {
            Id = run.Id,
            CreatedAtUtc = run.CreatedAtUtc,
            GeneratedAtUtc = run.GeneratedAtUtc,
            PeriodPreset = run.PeriodPreset,
            PeriodStartLocalDate = run.PeriodStartLocalDate,
            PeriodEndLocalDate = run.PeriodEndLocalDate,
            PeriodStartUtc = run.PeriodStartUtc,
            PeriodEndUtc = run.PeriodEndUtc,
            ScopeKind = run.ScopeKind,
            CashRegisterId = run.CashRegisterId,
            CashierId = run.CashierId,
            PaymentMethodFilter = run.PaymentMethodFilter,
            ActiveOnly = run.ActiveOnly,
            QueryParametersHash = run.QueryParametersHash,
            SnapshotHash = run.SnapshotHash,
            SnapshotSchemaVersion = run.SnapshotSchemaVersion,
            CreatedByUserId = run.CreatedByUserId,
            ExportProfileKey = run.ExportProfileKey,
            CorrelationId = run.CorrelationId,
            Warnings = warnings,
            Summary = summary
        });
    }

    private static string ComputeSha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
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

        // RKSV Monats-Nullbeleg: exclude from gross / tax operational totals.
        q = q.Where(p => p.RksvSpecialReceiptKind == null);

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
