using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Time;
using KasseAPI_Final.Services.FinanzOnlineIntegration;

namespace KasseAPI_Final.Services;

public sealed class MonatsberichtService : IMonatsberichtService
{
    public const string SnapshotSchemaVersion = "1.0";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly AppDbContext _db;
    private readonly IFinanzOnlineOutboxService _outbox;
    private readonly IAuditLogService _audit;
    private readonly IReportSubmissionCompatibilityService _submissionCompat;
    public MonatsberichtService(
        AppDbContext db,
        IFinanzOnlineOutboxService outbox,
        IReportSubmissionCompatibilityService submissionCompat,
        IAuditLogService audit)
    {
        _db = db;
        _outbox = outbox;
        _submissionCompat = submissionCompat;
        _audit = audit;
    }

    public async Task<MonatsberichtDto> GenerateOrRefreshProvisionalAsync(
        MonatsberichtGenerationRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
            throw new ArgumentException("Actor required.", nameof(actorUserId));

        var monthStart = NormalizeViennaMonthStart(request.ViennaMonthAnyDay);
        var scopeKind = (request.ScopeKind ?? MonatsberichtScopeKinds.Register).Trim();
        if (scopeKind != MonatsberichtScopeKinds.Register && scopeKind != MonatsberichtScopeKinds.Company)
            throw new InvalidOperationException("Invalid ScopeKind. Use Register or Company.");

        if (scopeKind == MonatsberichtScopeKinds.Register && !request.CashRegisterId.HasValue)
            throw new InvalidOperationException("CashRegisterId required for Register scope.");

        if (scopeKind == MonatsberichtScopeKinds.Company && request.CashRegisterId.HasValue)
            throw new InvalidOperationException("CashRegisterId must be null for Company scope.");

        if (scopeKind == MonatsberichtScopeKinds.Register &&
            !await _db.CashRegisters.AsNoTracking().AnyAsync(x => x.Id == request.CashRegisterId!.Value, cancellationToken))
            throw new InvalidOperationException("Cash register not found.");

        var existing = await _db.Set<MonatsberichtReport>()
            .Where(x =>
                x.ViennaMonthStart == monthStart &&
                x.ScopeKind == scopeKind &&
                (scopeKind == MonatsberichtScopeKinds.Company
                    ? x.CashRegisterId == null
                    : x.CashRegisterId == request.CashRegisterId) &&
                x.ReportStatus == MonatsberichtReportStatuses.Provisional &&
                x.SupersededByReportId == null)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing != null && !request.ForceNewProvisional)
        {
            var built = await BuildMonthlySnapshotAsync(monthStart, scopeKind, request.CashRegisterId, cancellationToken);
            existing.SnapshotJson = JsonSerializer.Serialize(built, JsonOpts);
            existing.SnapshotHash = ComputeSnapshotHash(built);
            existing.SnapshotSchemaVersion = SnapshotSchemaVersion;
            existing.SnapshotGrossSalesAmount = built.AggregationFromDaily.GrossSalesAmount;
            existing.StoreLabel = await ResolveStoreLabelAsync(scopeKind, request.CashRegisterId, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await _audit.LogSystemOperationAsync(
                "MonatsberichtSnapshotRefreshed",
                nameof(MonatsberichtReport),
                actorUserId,
                "ReportActor",
                description: "Monatsbericht provisional refreshed",
                requestData: new { existing.Id, monthStart, scopeKind },
                responseData: new { existing.SnapshotHash },
                status: AuditLogStatus.Success);
            return await MapToDtoAsync(existing.Id, cancellationToken) ?? throw new InvalidOperationException("Map failed.");
        }

        var summary = await BuildMonthlySnapshotAsync(monthStart, scopeKind, request.CashRegisterId, cancellationToken);

        var row = new MonatsberichtReport
        {
            ViennaMonthStart = monthStart,
            ScopeKind = scopeKind,
            CashRegisterId = scopeKind == MonatsberichtScopeKinds.Company ? null : request.CashRegisterId,
            StoreLabel = await ResolveStoreLabelAsync(scopeKind, request.CashRegisterId, cancellationToken),
            SnapshotJson = JsonSerializer.Serialize(summary, JsonOpts),
            SnapshotHash = ComputeSnapshotHash(summary),
            SnapshotSchemaVersion = SnapshotSchemaVersion,
            ReportStatus = MonatsberichtReportStatuses.Provisional,
            CorrectionKind = MonatsberichtCorrectionKinds.None,
            OriginalReportId = null,
            CorrectionOfReportId = null,
            ReportVersion = 1,
            ReportRevisionReason = "Initial generation",
            RebuildCause = null,
            CorrectionType = ReportCorrectionTypes.None,
            SubmissionImpact = ReportSubmissionImpacts.None,
            CreatedByUserId = actorUserId,
            SnapshotGrossSalesAmount = summary.AggregationFromDaily.GrossSalesAmount,
        };
        row.OriginalReportId = row.Id;

        _db.Set<MonatsberichtReport>().Add(row);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogSystemOperationAsync(
            "MonatsberichtGenerated",
            nameof(MonatsberichtReport),
            actorUserId,
            "ReportActor",
            description: "Monatsbericht provisional created",
            requestData: new { row.Id, monthStart, scopeKind },
            responseData: new { row.SnapshotHash },
            status: AuditLogStatus.Success);

        return await MapToDtoAsync(row.Id, cancellationToken) ?? throw new InvalidOperationException("Map failed.");
    }

    public async Task<MonatsberichtDto> FinalizeAsync(MonatsberichtFinalizeRequest request, string actorUserId, CancellationToken cancellationToken = default)
    {
        var row = await _db.Set<MonatsberichtReport>().FirstOrDefaultAsync(x => x.Id == request.ReportId, cancellationToken)
            ?? throw new InvalidOperationException("Monatsbericht not found.");

        if (row.ReportStatus == MonatsberichtReportStatuses.Finalized)
            throw new InvalidOperationException("Report already finalized.");

        if (row.ReportStatus == MonatsberichtReportStatuses.Superseded)
            throw new InvalidOperationException("Cannot finalize superseded report.");

        var summary = await BuildMonthlySnapshotAsync(row.ViennaMonthStart, row.ScopeKind, row.CashRegisterId, cancellationToken);

        row.SnapshotJson = JsonSerializer.Serialize(summary, JsonOpts);
        row.SnapshotHash = ComputeSnapshotHash(summary);
        row.SnapshotSchemaVersion = SnapshotSchemaVersion;
        row.ReportStatus = MonatsberichtReportStatuses.Finalized;
        row.FinalizedAtUtc = DateTime.UtcNow;
        row.FinalizedByUserId = actorUserId;
        row.ReportRevisionReason = request.Note ?? row.ReportRevisionReason ?? "Finalized";
        row.SnapshotGrossSalesAmount = summary.AggregationFromDaily.GrossSalesAmount;

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogSystemOperationAsync(
            "MonatsberichtFinalized",
            nameof(MonatsberichtReport),
            actorUserId,
            "ReportActor",
            description: request.Note ?? "Monatsbericht finalized",
            requestData: new { row.Id, row.SnapshotHash },
            status: AuditLogStatus.Success);

        return await MapToDtoAsync(row.Id, cancellationToken) ?? throw new InvalidOperationException("Map failed.");
    }

    public async Task<MonatsberichtDto> CreateCorrectionAsync(MonatsberichtCorrectionRequest request, string actorUserId, CancellationToken cancellationToken = default)
    {
        var prior = await _db.Set<MonatsberichtReport>().FirstOrDefaultAsync(x => x.Id == request.SupersedesReportId, cancellationToken)
            ?? throw new InvalidOperationException("Prior Monatsbericht not found.");

        if (prior.ReportStatus != MonatsberichtReportStatuses.Finalized)
            throw new InvalidOperationException("Correction requires finalized prior report.");

        if (prior.SupersededByReportId != null)
            throw new InvalidOperationException("Prior already superseded.");

        var duplicate = await _db.Set<MonatsberichtReport>().AsNoTracking()
            .AnyAsync(x => x.CorrectionOfReportId == prior.Id && x.SupersededByReportId == null, cancellationToken);
        if (duplicate)
            throw new InvalidOperationException("Duplicate correction request blocked for same prior report.");

        var summary = await BuildMonthlySnapshotAsync(prior.ViennaMonthStart, prior.ScopeKind, prior.CashRegisterId, cancellationToken);

        var row = new MonatsberichtReport
        {
            ViennaMonthStart = prior.ViennaMonthStart,
            ScopeKind = prior.ScopeKind,
            CashRegisterId = prior.CashRegisterId,
            StoreLabel = prior.StoreLabel,
            SnapshotJson = JsonSerializer.Serialize(summary, JsonOpts),
            SnapshotHash = ComputeSnapshotHash(summary),
            SnapshotSchemaVersion = SnapshotSchemaVersion,
            ReportStatus = MonatsberichtReportStatuses.Provisional,
            CorrectionKind = MonatsberichtCorrectionKinds.Rebuild,
            OriginalReportId = prior.OriginalReportId ?? prior.Id,
            CorrectionOfReportId = prior.Id,
            SupersedesReportId = prior.Id,
            ReportVersion = prior.ReportVersion + 1,
            ReportRevisionReason = request.Reason ?? "Correction rebuild from finalized report",
            RebuildCause = "manual_correction",
            CorrectionType = prior.LastSubmissionStatusCode == FinanzOnlineOutboxStatuses.ProtocolSuccess
                ? ReportCorrectionTypes.Amendment
                : ReportCorrectionTypes.Rebuild,
            SubmissionImpact = prior.LastSubmissionStatusCode == FinanzOnlineOutboxStatuses.ProtocolSuccess
                ? ReportSubmissionImpacts.SupersededAfterAccepted
                : prior.LastSubmissionStatusCode == FinanzOnlineOutboxStatuses.ProtocolFailure
                    ? ReportSubmissionImpacts.RejectedRebuild
                    : ReportSubmissionImpacts.RequiresResubmission,
            CreatedByUserId = actorUserId,
            SnapshotGrossSalesAmount = summary.AggregationFromDaily.GrossSalesAmount,
        };

        prior.SupersededByReportId = row.Id;
        prior.ReportStatus = MonatsberichtReportStatuses.Superseded;

        _db.Set<MonatsberichtReport>().Add(row);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogSystemOperationAsync(
            "MonatsberichtCorrectionCreated",
            nameof(MonatsberichtReport),
            actorUserId,
            "ReportActor",
            description: request.Reason ?? "Monatsbericht correction",
            requestData: new { priorReportId = prior.Id, newReportId = row.Id },
            status: AuditLogStatus.Success);

        return await MapToDtoAsync(row.Id, cancellationToken) ?? throw new InvalidOperationException("Map failed.");
    }

    public async Task<MonatsberichtDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await MapToDtoAsync(id, cancellationToken);

    public async Task<IReadOnlyList<MonatsberichtListItemDto>> ListAsync(
        DateTime? fromMonth,
        DateTime? toMonth,
        string? scopeKind,
        Guid? cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        var q = _db.Set<MonatsberichtReport>().AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(scopeKind))
            q = q.Where(x => x.ScopeKind == scopeKind.Trim());

        if (cashRegisterId.HasValue)
            q = q.Where(x => x.CashRegisterId == cashRegisterId.Value);

        if (fromMonth.HasValue)
        {
            var lo = NormalizeViennaMonthStart(fromMonth.Value);
            q = q.Where(x => x.ViennaMonthStart >= lo);
        }

        if (toMonth.HasValue)
        {
            var hi = NormalizeViennaMonthStart(toMonth.Value);
            q = q.Where(x => x.ViennaMonthStart <= hi);
        }

        var rows = await q
            .OrderByDescending(x => x.ViennaMonthStart)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);

        var regIds = rows.Where(x => x.CashRegisterId.HasValue).Select(x => x.CashRegisterId!.Value).Distinct().ToList();
        var regs = await _db.CashRegisters.AsNoTracking()
            .Where(r => regIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.RegisterNumber, cancellationToken);

        var list = new List<MonatsberichtListItemDto>();
        foreach (var x in rows)
        {
            string? regNo = null;
            if (x.CashRegisterId.HasValue)
                regs.TryGetValue(x.CashRegisterId.Value, out regNo);

            list.Add(new MonatsberichtListItemDto
            {
                Id = x.Id,
                ViennaMonthStart = x.ViennaMonthStart,
                ScopeKind = x.ScopeKind,
                CashRegisterId = x.CashRegisterId,
                RegisterNumber = regNo,
                ReportStatus = x.ReportStatus,
                CorrectionKind = x.CorrectionKind,
                ReportVersion = x.ReportVersion,
                SubmissionImpact = x.SubmissionImpact,
                GrossSalesAmount = x.SnapshotGrossSalesAmount,
                CreatedAtUtc = x.CreatedAtUtc,
                Submission = await BuildSubmissionStateAsync(x, cancellationToken),
            });
        }

        return list;
    }

    public async Task<MonatsberichtDto> SubmitToFinanzOnlineAsync(Guid reportId, string actorUserId, CancellationToken cancellationToken = default)
    {
        var row = await _db.Set<MonatsberichtReport>().FirstOrDefaultAsync(x => x.Id == reportId, cancellationToken)
            ?? throw new InvalidOperationException("Monatsbericht not found.");

        if (row.ReportStatus != MonatsberichtReportStatuses.Finalized)
            throw new InvalidOperationException("Only finalized Monatsbericht can be submitted.");

        var summary = JsonSerializer.Deserialize<MonatsberichtSummaryDto>(row.SnapshotJson, JsonOpts)
            ?? throw new InvalidOperationException("Snapshot corrupt.");

        var registerNumber = "COMPANY";
        if (row.CashRegisterId.HasValue)
        {
            var reg = await _db.CashRegisters.AsNoTracking().FirstAsync(x => x.Id == row.CashRegisterId.Value, cancellationToken);
            registerNumber = reg.RegisterNumber;
        }

        var mode = FinanzOnlineIntegrationMode.TEST;

        var payloadJson = JsonSerializer.Serialize(new
        {
            kind = "MonatsberichtMonthlySummary",
            reportId = row.Id,
            snapshotHash = row.SnapshotHash,
            schemaVersion = row.SnapshotSchemaVersion,
            viennaYearMonth = summary.ViennaYearMonth,
            scopeKind = row.ScopeKind,
            registerNumber,
            grossFromDaily = summary.AggregationFromDaily.GrossSalesAmount,
            grossFromRaw = summary.RawPaymentRollup.GrossSalesAmount,
        }, JsonOpts);

        var hashHex = ComputeSha256Hex(payloadJson);
        var ym = summary.ViennaYearMonth.Replace("-", "", StringComparison.Ordinal);
        var businessKey = $"{row.ScopeKind}|{ym}|{registerNumber}|{row.Id:N}";

        var msg = await _outbox.EnqueueSubmissionAsync(
            aggregateType: "MonatsberichtReport",
            aggregateId: row.Id,
            messageType: FinanzOnlineMonatsberichtMessageTypes.MonatsberichtMonthlySummary,
            businessKey: businessKey,
            payload: new FinanzOnlineOutboxPayload
            {
                Mode = mode,
                Scope = new FinanzOnlineScope { RegisterId = registerNumber },
                Correlation = new FinanzOnlineCorrelationContext
                {
                    BusinessKey = businessKey,
                    PayloadHash = hashHex,
                    CorrelationId = row.Id.ToString("N")
                },
                SubmissionKind = FinanzOnlineSubmissionKind.Register,
                PayloadJson = payloadJson
            },
            cancellationToken);

        row.LastFinanzOnlineOutboxMessageId = msg.Id;
        row.LastSubmissionStatusCode = msg.Status;
        row.LastSubmissionError = msg.LastErrorMessage;
        row.SubmissionImpact = ReportSubmissionImpacts.RequiresResubmission;
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogSystemOperationAsync(
            "MonatsberichtFinanzOnlineSubmit",
            nameof(MonatsberichtReport),
            actorUserId,
            "ReportActor",
            description: "Monatsbericht enqueued to FinanzOnline outbox",
            requestData: new { reportId = row.Id, outboxMessageId = msg.Id },
            status: AuditLogStatus.Success);

        return await MapToDtoAsync(row.Id, cancellationToken) ?? throw new InvalidOperationException("Map failed.");
    }

    private async Task<MonatsberichtSummaryDto> BuildMonthlySnapshotAsync(
        DateTime monthStart,
        string scopeKind,
        Guid? cashRegisterId,
        CancellationToken cancellationToken)
    {
        var lastDay = monthStart.AddMonths(1).AddDays(-1);
        var (fromUtc, toExclusiveUtc) = PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(monthStart, lastDay);

        var ym = $"{monthStart.Year:D4}-{monthStart.Month:D2}";
        var expectedDays = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);

        IQueryable<TagesberichtReport> dailyQ = _db.Set<TagesberichtReport>().AsNoTracking()
            .Where(x =>
                x.ReportStatus == TagesberichtReportStatuses.Finalized &&
                x.SupersededByReportId == null &&
                (x.OperatorUserIdScope == null || x.OperatorUserIdScope == string.Empty) &&
                x.ViennaBusinessDate >= monthStart &&
                x.ViennaBusinessDate <= lastDay);

        if (scopeKind == MonatsberichtScopeKinds.Register)
            dailyQ = dailyQ.Where(x => x.CashRegisterId == cashRegisterId!.Value);

        var dailyRows = await dailyQ
            .OrderBy(x => x.ViennaBusinessDate)
            .ToListAsync(cancellationToken);

        var linked = new List<LinkedTagesberichtLineDto>();
        var taxMerge = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var methodMerge = new Dictionary<string, (int Count, decimal Total)>(StringComparer.Ordinal);

        decimal sumGross = 0, sumTax = 0, sumRefund = 0;
        int sumSaleRows = 0, sumRefRows = 0, sumStorno = 0;

        foreach (var d in dailyRows)
        {
            var snap = JsonSerializer.Deserialize<TagesberichtSummaryDto>(d.SnapshotJson, JsonOpts);
            if (snap == null) continue;

            sumGross += snap.GrossSalesAmount;
            sumTax += snap.TaxTotalAmount;
            sumRefund += snap.RefundAmountTotal;
            sumSaleRows += snap.SalePaymentRowCount;
            sumRefRows += snap.RefundRowCount;
            sumStorno += snap.StornoRowCount;

            var reg = await _db.CashRegisters.AsNoTracking().FirstOrDefaultAsync(x => x.Id == d.CashRegisterId, cancellationToken);
            linked.Add(new LinkedTagesberichtLineDto
            {
                TagesberichtId = d.Id,
                ViennaBusinessDate = d.ViennaBusinessDate,
                CashRegisterId = d.CashRegisterId,
                RegisterNumber = reg?.RegisterNumber,
                SnapshotHash = d.SnapshotHash,
                GrossSalesAmount = snap.GrossSalesAmount
            });

            foreach (var t in snap.TaxBreakdown)
                taxMerge[t.TaxBucketKey] = taxMerge.GetValueOrDefault(t.TaxBucketKey) + t.TaxAmount;

            foreach (var m in snap.PaymentMethodBreakdown)
            {
                var key = m.MethodKey;
                var cur = methodMerge.GetValueOrDefault(key);
                methodMerge[key] = (cur.Count + m.RowCount, cur.Total + m.TotalAmount);
            }
        }

        var distinctDays = dailyRows.Select(x => x.ViennaBusinessDate.Date).Distinct().Count();

        var raw = await RollupRawPaymentsAsync(scopeKind, cashRegisterId, fromUtc, toExclusiveUtc, cancellationToken);

        var deltaGross = sumGross - raw.GrossSalesAmount;
        var adj = new MonatsberichtAdjustmentDto
        {
            GrossDeltaDailyVsRaw = deltaGross,
            RequiresReview = Math.Abs(deltaGross) > 0.05m,
            NoteDe = Math.Abs(deltaGross) > 0.05m
                ? "Summe der Tagesberichte weicht von Rohzahlungen ab — Abstimmung prüfen."
                : null
        };

        var warnings = new List<string>
        {
            "aggregation:sum_of_finalized_tagesberichte",
            "reconciliation:raw_payment_details_month_range"
        };

        if (distinctDays < expectedDays)
            warnings.Add($"missing_calendar_days:{expectedDays - distinctDays}");

        if (dailyRows.Count == 0)
            warnings.Add("no_linked_daily_tagesberichte");

        if (Math.Abs(deltaGross) > 0.05m)
            warnings.Add("daily_aggregate_vs_raw_payment_mismatch");

        var viennaNow = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
        if (monthStart.Year == viennaNow.Year && monthStart.Month == viennaNow.Month)
            warnings.Add("provisional_current_month_incomplete");

        var paymentBreakdown = methodMerge
            .Select(kv => new TagesberichtPaymentMethodBreakdownDto
            {
                MethodKey = kv.Key,
                DisplayLabel = TryPaymentMethodLabel(kv.Key),
                RowCount = kv.Value.Count,
                TotalAmount = kv.Value.Total
            })
            .OrderByDescending(x => x.TotalAmount)
            .ToList();

        var taxBreakdown = taxMerge
            .Select(kv => new TagesberichtTaxBreakdownDto
            {
                TaxBucketKey = kv.Key,
                TaxAmount = kv.Value,
                NetHint = 0
            })
            .OrderByDescending(x => x.TaxAmount)
            .ToList();

        string? storeLabel = await ResolveStoreLabelAsync(scopeKind, cashRegisterId, cancellationToken);
        string? regNo = null;
        if (cashRegisterId.HasValue)
        {
            var r = await _db.CashRegisters.AsNoTracking().FirstOrDefaultAsync(x => x.Id == cashRegisterId.Value, cancellationToken);
            regNo = r?.RegisterNumber;
        }

        return new MonatsberichtSummaryDto
        {
            SchemaVersion = SnapshotSchemaVersion,
            ViennaYearMonth = ym,
            PeriodStartUtc = fromUtc,
            PeriodEndUtcExclusive = toExclusiveUtc,
            ScopeKind = scopeKind,
            CashRegisterId = cashRegisterId,
            RegisterNumber = regNo,
            StoreLabel = storeLabel,
            LinkedFinalizedTagesberichte = linked,
            AggregationFromDaily = new MonatsberichtAggregationFromDailyDto
            {
                LinkedDailyReportCount = dailyRows.Count,
                ExpectedCalendarDaysInMonth = expectedDays,
                DistinctDaysCovered = distinctDays,
                GrossSalesAmount = sumGross,
                TaxTotalAmount = sumTax,
                RefundAmountTotal = sumRefund,
                SalePaymentRowCount = sumSaleRows,
                RefundRowCount = sumRefRows,
                StornoRowCount = sumStorno
            },
            RawPaymentRollup = raw,
            Adjustment = adj,
            PaymentMethodBreakdown = paymentBreakdown,
            TaxBreakdown = taxBreakdown,
            Warnings = warnings
        };
    }

    private async Task<MonatsberichtRawPaymentRollupDto> RollupRawPaymentsAsync(
        string scopeKind,
        Guid? cashRegisterId,
        DateTime fromUtc,
        DateTime toExclusiveUtc,
        CancellationToken cancellationToken)
    {
        IQueryable<PaymentDetails> q = _db.PaymentDetails.AsNoTracking()
            .Where(p => p.CreatedAt >= fromUtc && p.CreatedAt < toExclusiveUtc);

        if (scopeKind == MonatsberichtScopeKinds.Register)
            q = q.Where(p => p.CashRegisterId == cashRegisterId!.Value);
        else
        {
            var ids = await _db.CashRegisters.AsNoTracking().Select(x => x.Id).ToListAsync(cancellationToken);
            q = q.Where(p => ids.Contains(p.CashRegisterId));
        }

        var payments = await q.ToListAsync(cancellationToken);
        var saleLike = payments.Where(p => p.IsActive && !p.IsRefund && !p.IsStorno).ToList();
        var refunds = payments.Where(p => p.IsActive && p.IsRefund).ToList();

        return new MonatsberichtRawPaymentRollupDto
        {
            GrossSalesAmount = saleLike.Sum(p => p.TotalAmount),
            TaxTotalAmount = saleLike.Sum(p => p.TaxAmount),
            RefundAmountTotal = refunds.Sum(p => p.TotalAmount),
            SalePaymentRowCount = saleLike.Count
        };
    }

    private async Task<string?> ResolveStoreLabelAsync(string scopeKind, Guid? cashRegisterId, CancellationToken cancellationToken)
    {
        if (scopeKind == MonatsberichtScopeKinds.Company)
            return "Company";

        if (!cashRegisterId.HasValue) return null;

        return await _db.CashRegisters.AsNoTracking()
            .Where(x => x.Id == cashRegisterId.Value)
            .Select(x => x.Location)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string? TryPaymentMethodLabel(string raw)
    {
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) &&
            Enum.IsDefined(typeof(PaymentMethod), n))
            return ((PaymentMethod)n).ToString();
        return null;
    }

    private async Task<TagesberichtSubmissionStateDto> BuildSubmissionStateAsync(
        MonatsberichtReport row,
        CancellationToken cancellationToken)
    {
        var envelope = await _submissionCompat.BuildEnvelopeAsync(new BuildReportSubmissionEnvelopeRequest
        {
            ReportType = "Monatsbericht",
            ReportId = row.Id,
            ReportState = row.ReportStatus,
            OutboxMessageId = row.LastFinanzOnlineOutboxMessageId,
            SupersedesReportId = row.SupersedesReportId,
            SupersededByReportId = row.SupersededByReportId
        }, cancellationToken);
        return _submissionCompat.ToLegacySubmissionState(envelope);
    }

    private async Task<MonatsberichtDto?> MapToDtoAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await _db.Set<MonatsberichtReport>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (row == null) return null;

        var summary = JsonSerializer.Deserialize<MonatsberichtSummaryDto>(row.SnapshotJson, JsonOpts) ?? new MonatsberichtSummaryDto();
        string? regNo = null;
        if (row.CashRegisterId.HasValue)
        {
            var reg = await _db.CashRegisters.AsNoTracking().FirstOrDefaultAsync(x => x.Id == row.CashRegisterId.Value, cancellationToken);
            regNo = reg?.RegisterNumber;
        }

        var envelope = await _submissionCompat.BuildEnvelopeAsync(new BuildReportSubmissionEnvelopeRequest
        {
            ReportType = "Monatsbericht",
            ReportId = row.Id,
            ReportState = row.ReportStatus,
            OutboxMessageId = row.LastFinanzOnlineOutboxMessageId,
            SupersedesReportId = row.SupersedesReportId,
            SupersededByReportId = row.SupersededByReportId
        }, cancellationToken);
        var submission = _submissionCompat.ToLegacySubmissionState(envelope);

        return new MonatsberichtDto
        {
            Id = row.Id,
            ViennaMonthStart = row.ViennaMonthStart,
            ScopeKind = row.ScopeKind,
            CashRegisterId = row.CashRegisterId,
            RegisterNumber = regNo,
            StoreLabel = row.StoreLabel,
            ReportStatus = row.ReportStatus,
            CorrectionKind = row.CorrectionKind,
            OriginalReportId = row.OriginalReportId,
            CorrectionOfReportId = row.CorrectionOfReportId,
            SupersedesReportId = row.SupersedesReportId,
            SupersededByReportId = row.SupersededByReportId,
            ReportVersion = row.ReportVersion,
            ReportRevisionReason = row.ReportRevisionReason,
            RebuildCause = row.RebuildCause,
            CorrectionType = row.CorrectionType,
            SubmissionImpact = row.SubmissionImpact,
            CreatedAtUtc = row.CreatedAtUtc,
            CreatedByUserId = row.CreatedByUserId,
            FinalizedAtUtc = row.FinalizedAtUtc,
            FinalizedByUserId = row.FinalizedByUserId,
            SnapshotSchemaVersion = row.SnapshotSchemaVersion,
            SnapshotHash = row.SnapshotHash,
            Summary = summary,
            Submission = submission,
            SubmissionEnvelope = envelope,
            Correction = new TagesberichtCorrectionInfoDto
            {
                IsCorrection = row.SupersedesReportId != null,
                SupersedesReportId = row.SupersedesReportId,
                SupersededByReportId = row.SupersededByReportId
            },
            ExportProfiles = BuildExportProfiles()
        };
    }

    private static IReadOnlyList<TagesberichtExportProfileDto> BuildExportProfiles() =>
        new[]
        {
            new TagesberichtExportProfileDto
            {
                ProfileKey = "operationalPreview",
                LabelDe = "Operational Preview",
                DescriptionDe = "Operative Monatsansicht; keine offizielle Buchhaltungs- oder Rechtsausgabe.",
                NonLegalOutput = true,
                IsLegalProfile = false,
                IsDiagnosticOnly = false,
                IncludeTraceIds = false,
                IncludeTechnicalHashes = false,
                IncludeReconciliationWarnings = true
            },
            new TagesberichtExportProfileDto
            {
                ProfileKey = "accountingReport",
                LabelDe = "Accounting Report",
                DescriptionDe = "Für Buchhaltung und Monatsabgleich; nicht als Rechtsnachweis verwenden.",
                NonLegalOutput = true,
                IsLegalProfile = false,
                IsDiagnosticOnly = false,
                IncludeTraceIds = false,
                IncludeTechnicalHashes = true,
                IncludeReconciliationWarnings = true
            },
            new TagesberichtExportProfileDto
            {
                ProfileKey = "legalComplianceExport",
                LabelDe = "Legal Compliance Export",
                DescriptionDe = "Für Compliance/Prüfung; nur bei vollständigen Daten als offizieller Export verwenden.",
                NonLegalOutput = false,
                IsLegalProfile = true,
                IsDiagnosticOnly = false,
                IncludeTraceIds = true,
                IncludeTechnicalHashes = true,
                IncludeReconciliationWarnings = true
            },
            new TagesberichtExportProfileDto
            {
                ProfileKey = "diagnosticPackage",
                LabelDe = "Diagnostic Package",
                DescriptionDe = "Nur technische Diagnose; ausdrücklich kein offizielles Dokument.",
                NonLegalOutput = true,
                IsLegalProfile = false,
                IsDiagnosticOnly = true,
                IncludeTraceIds = true,
                IncludeTechnicalHashes = true,
                IncludeReconciliationWarnings = true
            }
        };

    private static DateTime NormalizeViennaMonthStart(DateTime d)
    {
        return new DateTime(d.Year, d.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
    }

    private static string ComputeSnapshotHash(MonatsberichtSummaryDto summary)
    {
        var json = JsonSerializer.Serialize(summary, JsonOpts);
        return ComputeSha256Hex(json);
    }

    private static string ComputeSha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

/// <summary>TagesberichtService ile aynı outbox yaşam döngüsü metinleri (paylaşım için minimal statik sınıf).</summary>
internal static class TagesberichtServiceMap
{
    internal static string MapOutboxToLifecycle(string status)
    {
        return status switch
        {
            FinanzOnlineOutboxStatuses.Pending => "queued",
            FinanzOnlineOutboxStatuses.Processing => "pending",
            FinanzOnlineOutboxStatuses.RetryableFailure => "retry_pending",
            FinanzOnlineOutboxStatuses.AwaitingProtocol => "awaiting_protocol",
            FinanzOnlineOutboxStatuses.ProtocolSuccess => "accepted",
            FinanzOnlineOutboxStatuses.ProtocolFailure => "rejected",
            FinanzOnlineOutboxStatuses.PermanentFailure => "rejected",
            FinanzOnlineOutboxStatuses.ManualReviewRequired => "correction_required",
            FinanzOnlineOutboxStatuses.DeadLetter => "failed_terminal",
            _ => "unknown"
        };
    }

    internal static string? MapOperatorHintDe(string status, string? err)
    {
        if (status == FinanzOnlineOutboxStatuses.ProtocolSuccess)
            return "Übermittlung erfolgreich (Outbox).";
        if (status == FinanzOnlineOutboxStatuses.RetryableFailure)
            return "Vorübergehender Fehler — automatischer Wiederholungsversuch läuft.";
        if (status is FinanzOnlineOutboxStatuses.DeadLetter or FinanzOnlineOutboxStatuses.PermanentFailure or FinanzOnlineOutboxStatuses.ProtocolFailure)
            return string.IsNullOrWhiteSpace(err) ? "Dauerhafte Ablehnung oder Fehler — bitte prüfen." : err;
        if (status == FinanzOnlineOutboxStatuses.ManualReviewRequired)
            return "Manuelle Prüfung erforderlich.";
        if (status == FinanzOnlineOutboxStatuses.AwaitingProtocol)
            return "Warte auf Protokollbestätigung.";
        return null;
    }
}
