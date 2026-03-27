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

public sealed class TagesberichtService : ITagesberichtService
{
    public const string SnapshotSchemaVersion = "1.0";
    private const int MaxTracePaymentIds = 5000;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly AppDbContext _db;
    private readonly ITagesabschlussService _tagesabschluss;
    private readonly IFinanzOnlineOutboxService _outbox;
    private readonly IAuditLogService _audit;
    private readonly IReportSubmissionCompatibilityService _submissionCompat;
    private readonly ILogger<TagesberichtService> _logger;

    public TagesberichtService(
        AppDbContext db,
        ITagesabschlussService tagesabschluss,
        IFinanzOnlineOutboxService outbox,
        IReportSubmissionCompatibilityService submissionCompat,
        IAuditLogService audit,
        ILogger<TagesberichtService> logger)
    {
        _db = db;
        _tagesabschluss = tagesabschluss;
        _outbox = outbox;
        _submissionCompat = submissionCompat;
        _audit = audit;
        _logger = logger;
    }

    public async Task<TagesberichtDto> GenerateOrRefreshProvisionalAsync(
        TagesberichtGenerationRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
            throw new ArgumentException("Actor required.", nameof(actorUserId));

        var viennaDate = NormalizeViennaDate(request.ViennaBusinessDate);
        var (fromUtc, toExclusive) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(viennaDate);

        if (!await _db.CashRegisters.AsNoTracking().AnyAsync(x => x.Id == request.CashRegisterId, cancellationToken))
            throw new InvalidOperationException("Cash register not found.");

        var scopeKey = request.OperatorUserIdScope?.Trim();
        var existing = await _db.Set<TagesberichtReport>()
            .Where(x =>
                x.ViennaBusinessDate == viennaDate &&
                x.CashRegisterId == request.CashRegisterId &&
                (scopeKey == null ? x.OperatorUserIdScope == null : x.OperatorUserIdScope == scopeKey) &&
                x.ReportStatus == TagesberichtReportStatuses.Provisional &&
                x.SupersededByReportId == null)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing != null && !request.ForceNewProvisional)
        {
            var built = await BuildSnapshotAsync(
                viennaDate, request.CashRegisterId, scopeKey, fromUtc, toExclusive, cancellationToken);
            existing.SnapshotJson = JsonSerializer.Serialize(built, JsonOpts);
            existing.SnapshotHash = ComputeSnapshotHash(built);
            existing.SnapshotSchemaVersion = SnapshotSchemaVersion;
            existing.SnapshotGrossSalesAmount = built.GrossSalesAmount;
            await _db.SaveChangesAsync(cancellationToken);
            await _audit.LogSystemOperationAsync(
                "TagesberichtSnapshotRefreshed",
                nameof(TagesberichtReport),
                actorUserId,
                "ReportActor",
                description: "Tagesbericht provisional snapshot refreshed",
                requestData: new { existing.Id, viennaDate, request.CashRegisterId },
                responseData: new { existing.SnapshotHash },
                status: AuditLogStatus.Success);
            return await MapToDtoAsync(existing.Id, cancellationToken) ?? throw new InvalidOperationException("Map failed.");
        }

        var summary = await BuildSnapshotAsync(
            viennaDate, request.CashRegisterId, scopeKey, fromUtc, toExclusive, cancellationToken);

        var row = new TagesberichtReport
        {
            ViennaBusinessDate = viennaDate,
            CashRegisterId = request.CashRegisterId,
            StoreLabel = await ResolveStoreLabelAsync(request.CashRegisterId, cancellationToken),
            OperatorUserIdScope = scopeKey,
            SnapshotJson = JsonSerializer.Serialize(summary, JsonOpts),
            SnapshotHash = ComputeSnapshotHash(summary),
            SnapshotSchemaVersion = SnapshotSchemaVersion,
            ReportStatus = TagesberichtReportStatuses.Provisional,
            CorrectionKind = TagesberichtCorrectionKinds.None,
            OriginalReportId = null,
            CorrectionOfReportId = null,
            ReportVersion = 1,
            ReportRevisionReason = "Initial generation",
            RebuildCause = null,
            CorrectionType = ReportCorrectionTypes.None,
            SubmissionImpact = ReportSubmissionImpacts.None,
            CreatedByUserId = actorUserId,
            SnapshotGrossSalesAmount = summary.GrossSalesAmount,
        };
        row.OriginalReportId = row.Id;

        _db.Set<TagesberichtReport>().Add(row);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogSystemOperationAsync(
            "TagesberichtGenerated",
            nameof(TagesberichtReport),
            actorUserId,
            "ReportActor",
            description: "Tagesbericht provisional report created",
            requestData: new { row.Id, viennaDate, request.CashRegisterId },
            responseData: new { row.SnapshotHash },
            status: AuditLogStatus.Success);

        return await MapToDtoAsync(row.Id, cancellationToken) ?? throw new InvalidOperationException("Map failed.");
    }

    public async Task<TagesberichtDto> FinalizeAsync(TagesberichtFinalizeRequest request, string actorUserId, CancellationToken cancellationToken = default)
    {
        var row = await _db.Set<TagesberichtReport>().FirstOrDefaultAsync(x => x.Id == request.ReportId, cancellationToken)
            ?? throw new InvalidOperationException("Tagesbericht not found.");

        if (row.ReportStatus == TagesberichtReportStatuses.Finalized)
            throw new InvalidOperationException("Report already finalized (duplicate finalize blocked).");

        if (row.ReportStatus == TagesberichtReportStatuses.Superseded)
            throw new InvalidOperationException("Cannot finalize a superseded report.");

        var viennaDate = NormalizeViennaDate(row.ViennaBusinessDate);
        var (fromUtc, toExclusive) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(viennaDate);
        var summary = await BuildSnapshotAsync(
            viennaDate,
            row.CashRegisterId,
            row.OperatorUserIdScope,
            fromUtc,
            toExclusive,
            cancellationToken);

        row.SnapshotJson = JsonSerializer.Serialize(summary, JsonOpts);
        row.SnapshotHash = ComputeSnapshotHash(summary);
        row.SnapshotSchemaVersion = SnapshotSchemaVersion;
        row.ReportStatus = TagesberichtReportStatuses.Finalized;
        row.FinalizedAtUtc = DateTime.UtcNow;
        row.FinalizedByUserId = actorUserId;
        row.ReportRevisionReason = request.Note ?? row.ReportRevisionReason ?? "Finalized";
        row.SnapshotGrossSalesAmount = summary.GrossSalesAmount;

        var anchorUtc = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(viennaDate);
        var anchorLabel = PostgreSqlUtcDateTime.FormatViennaUtcInstantAsYyyyMmDd(anchorUtc);
        var closings = await _db.DailyClosings.AsNoTracking()
            .Where(c => c.ClosingType == "Daily" && c.CashRegisterId == row.CashRegisterId)
            .ToListAsync(cancellationToken);
        var closingMatch = closings.FirstOrDefault(c =>
            PostgreSqlUtcDateTime.FormatViennaUtcInstantAsYyyyMmDd(c.ClosingDate) == anchorLabel);
        if (closingMatch != null)
            row.LinkedDailyClosingId = closingMatch.Id;

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogSystemOperationAsync(
            "TagesberichtFinalized",
            nameof(TagesberichtReport),
            actorUserId,
            "ReportActor",
            description: request.Note ?? "Tagesbericht finalized (immutable snapshot)",
            requestData: new { row.Id, row.SnapshotHash },
            status: AuditLogStatus.Success);

        return await MapToDtoAsync(row.Id, cancellationToken) ?? throw new InvalidOperationException("Map failed.");
    }

    public async Task<TagesberichtDto> CreateCorrectionAsync(TagesberichtCorrectionRequest request, string actorUserId, CancellationToken cancellationToken = default)
    {
        var prior = await _db.Set<TagesberichtReport>().FirstOrDefaultAsync(x => x.Id == request.SupersedesReportId, cancellationToken)
            ?? throw new InvalidOperationException("Prior Tagesbericht not found.");

        if (prior.ReportStatus != TagesberichtReportStatuses.Finalized)
            throw new InvalidOperationException("Correction requires a finalized prior report.");

        if (prior.SupersededByReportId != null)
            throw new InvalidOperationException("Prior report already has a successor.");

        var duplicate = await _db.Set<TagesberichtReport>().AsNoTracking()
            .AnyAsync(x => x.CorrectionOfReportId == prior.Id && x.SupersededByReportId == null, cancellationToken);
        if (duplicate)
            throw new InvalidOperationException("Duplicate correction request blocked for same prior report.");

        var viennaDate = NormalizeViennaDate(prior.ViennaBusinessDate);
        var (fromUtc, toExclusive) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(viennaDate);
        var summary = await BuildSnapshotAsync(
            viennaDate,
            prior.CashRegisterId,
            prior.OperatorUserIdScope,
            fromUtc,
            toExclusive,
            cancellationToken);

        var row = new TagesberichtReport
        {
            ViennaBusinessDate = viennaDate,
            CashRegisterId = prior.CashRegisterId,
            StoreLabel = prior.StoreLabel,
            OperatorUserIdScope = prior.OperatorUserIdScope,
            SnapshotJson = JsonSerializer.Serialize(summary, JsonOpts),
            SnapshotHash = ComputeSnapshotHash(summary),
            SnapshotSchemaVersion = SnapshotSchemaVersion,
            ReportStatus = TagesberichtReportStatuses.Provisional,
            CorrectionKind = TagesberichtCorrectionKinds.Rebuild,
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
            SnapshotGrossSalesAmount = summary.GrossSalesAmount,
        };

        prior.SupersededByReportId = row.Id;
        prior.ReportStatus = TagesberichtReportStatuses.Superseded;

        _db.Set<TagesberichtReport>().Add(row);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogSystemOperationAsync(
            "TagesberichtCorrectionCreated",
            nameof(TagesberichtReport),
            actorUserId,
            "ReportActor",
            description: request.Reason ?? "Tagesbericht correction chain",
            requestData: new { priorReportId = prior.Id, newReportId = row.Id },
            status: AuditLogStatus.Success);

        return await MapToDtoAsync(row.Id, cancellationToken) ?? throw new InvalidOperationException("Map failed.");
    }

    public async Task<TagesberichtDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await MapToDtoAsync(id, cancellationToken);

    public async Task<IReadOnlyList<TagesberichtListItemDto>> ListAsync(
        DateTime? fromDate,
        DateTime? toDate,
        Guid? cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        var q = _db.Set<TagesberichtReport>().AsNoTracking().AsQueryable();
        if (cashRegisterId.HasValue)
            q = q.Where(x => x.CashRegisterId == cashRegisterId.Value);

        if (fromDate.HasValue)
        {
            var lo = NormalizeViennaDate(fromDate.Value);
            q = q.Where(x => x.ViennaBusinessDate >= lo);
        }

        if (toDate.HasValue)
        {
            var hi = NormalizeViennaDate(toDate.Value);
            q = q.Where(x => x.ViennaBusinessDate <= hi);
        }

        var rows = await q
            .OrderByDescending(x => x.ViennaBusinessDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);

        var regIds = rows.Select(x => x.CashRegisterId).Distinct().ToList();
        var regs = await _db.CashRegisters.AsNoTracking()
            .Where(r => regIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.RegisterNumber, cancellationToken);

        var list = new List<TagesberichtListItemDto>();
        foreach (var x in rows)
        {
            regs.TryGetValue(x.CashRegisterId, out var regNo);
            list.Add(new TagesberichtListItemDto
            {
                Id = x.Id,
                ViennaBusinessDate = x.ViennaBusinessDate,
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

    public async Task<TagesberichtDto> SubmitToFinanzOnlineAsync(Guid reportId, string actorUserId, CancellationToken cancellationToken = default)
    {
        var row = await _db.Set<TagesberichtReport>().FirstOrDefaultAsync(x => x.Id == reportId, cancellationToken)
            ?? throw new InvalidOperationException("Tagesbericht not found.");

        if (row.ReportStatus != TagesberichtReportStatuses.Finalized)
            throw new InvalidOperationException("Only finalized Tagesbericht can be submitted to FinanzOnline.");

        var summary = JsonSerializer.Deserialize<TagesberichtSummaryDto>(row.SnapshotJson, JsonOpts)
            ?? throw new InvalidOperationException("Snapshot corrupt.");

        var register = await _db.CashRegisters.AsNoTracking().FirstAsync(x => x.Id == row.CashRegisterId, cancellationToken);
        // Bilgilendirici özet hattı: gerçek RKDB üretimi yok; PROD kesim koruması ve SOAP zorunluluğundan kaçınmak için TEST modu.
        var mode = FinanzOnlineIntegrationMode.TEST;

        var payloadJson = JsonSerializer.Serialize(new
        {
            kind = "TagesberichtDailySummary",
            reportId = row.Id,
            snapshotHash = row.SnapshotHash,
            schemaVersion = row.SnapshotSchemaVersion,
            viennaDate = row.ViennaBusinessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            registerNumber = register.RegisterNumber,
            gross = summary.GrossSalesAmount,
            tax = summary.TaxTotalAmount,
            refunds = summary.RefundAmountTotal,
        }, JsonOpts);

        var hashHex = ComputeSha256Hex(payloadJson);
        var businessKey = $"{row.CashRegisterId:N}|{PostgreSqlUtcDateTime.FormatViennaUtcInstantAsYyyyMmDd(PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(NormalizeViennaDate(row.ViennaBusinessDate)))}|{row.Id:N}";

        var msg = await _outbox.EnqueueSubmissionAsync(
            aggregateType: "TagesberichtReport",
            aggregateId: row.Id,
            messageType: FinanzOnlineTagesberichtMessageTypes.TagesberichtDailySummary,
            businessKey: businessKey,
            payload: new FinanzOnlineOutboxPayload
            {
                Mode = mode,
                Scope = new FinanzOnlineScope
                {
                    RegisterId = register.RegisterNumber
                },
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
            "TagesberichtFinanzOnlineSubmit",
            nameof(TagesberichtReport),
            actorUserId,
            "ReportActor",
            description: "Tagesbericht submission enqueued to FinanzOnline outbox",
            requestData: new { reportId = row.Id, outboxMessageId = msg.Id },
            status: AuditLogStatus.Success);

        return await MapToDtoAsync(row.Id, cancellationToken) ?? throw new InvalidOperationException("Map failed.");
    }

    private async Task<TagesberichtSummaryDto> BuildSnapshotAsync(
        DateTime viennaDate,
        Guid cashRegisterId,
        string? operatorScope,
        DateTime fromUtc,
        DateTime toExclusiveUtc,
        CancellationToken cancellationToken)
    {
        var q = _db.PaymentDetails.AsNoTracking()
            .Where(p => p.CashRegisterId == cashRegisterId &&
                        p.CreatedAt >= fromUtc &&
                        p.CreatedAt < toExclusiveUtc);

        if (!string.IsNullOrWhiteSpace(operatorScope))
            q = q.Where(p => p.CashierId == operatorScope);

        var payments = await q.ToListAsync(cancellationToken);

        var saleLike = payments.Where(p => p.IsActive && !p.IsRefund && !p.IsStorno).ToList();
        var refunds = payments.Where(p => p.IsActive && p.IsRefund).ToList();
        var stornos = payments.Where(p => p.IsActive && p.IsStorno).ToList();

        var gross = saleLike.Sum(p => p.TotalAmount);
        var taxTotal = saleLike.Sum(p => p.TaxAmount);
        var refundTotal = refunds.Sum(p => p.TotalAmount);

        var taxBuckets = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var p in saleLike)
        {
            MergeTaxDetails(p.TaxAmount, p.TaxDetails, taxBuckets);
        }

        var methodBuckets = saleLike
            .GroupBy(p => p.PaymentMethodRaw)
            .Select(g => new TagesberichtPaymentMethodBreakdownDto
            {
                MethodKey = g.Key,
                DisplayLabel = TryPaymentMethodLabel(g.Key),
                RowCount = g.Count(),
                TotalAmount = g.Sum(x => x.TotalAmount)
            })
            .OrderByDescending(x => x.TotalAmount)
            .ToList();

        var unknownMethod = saleLike.Count(p => !int.TryParse(p.PaymentMethodRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out _));

        var paymentsWithoutInvoice = await _tagesabschluss.GetPaymentsWithoutInvoiceCountAsync(
            cashRegisterId, fromUtc, toExclusiveUtc);

        var offlineLinked = saleLike.Count(p => p.OfflineTransactionId != null);

        var register = await _db.CashRegisters.AsNoTracking().FirstOrDefaultAsync(x => x.Id == cashRegisterId, cancellationToken);
        var dailyClosing = await _db.DailyClosings.AsNoTracking()
            .Where(c => c.CashRegisterId == cashRegisterId && c.ClosingType == "Daily")
            .OrderByDescending(c => c.ClosingDate)
            .FirstOrDefaultAsync(cancellationToken);

        var dayClosed = dailyClosing != null &&
                        PostgreSqlUtcDateTime.FormatViennaUtcInstantAsYyyyMmDd(dailyClosing.ClosingDate) ==
                        PostgreSqlUtcDateTime.FormatViennaUtcInstantAsYyyyMmDd(
                            PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(viennaDate));

        var traceIds = payments.Select(p => p.Id).OrderBy(x => x).Take(MaxTracePaymentIds).ToList();
        var traceHash = ComputeSha256Hex(string.Join(',', traceIds.Select(x => x.ToString("N"))));

        var warnings = new List<string> { "snapshot_source:payment_details_active_rows" };
        if (paymentsWithoutInvoice > 0)
            warnings.Add($"payments_without_invoice:{paymentsWithoutInvoice}");
        if (unknownMethod > 0)
            warnings.Add($"unknown_payment_method_rows:{unknownMethod}");
        if (payments.Count >= MaxTracePaymentIds)
            warnings.Add("trace_payment_ids_truncated");

        var viennaNow = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
        if (viennaDate.Date >= viennaNow.Date)
            warnings.Add("provisional_day_not_complete");

        var taxBreakdown = taxBuckets
            .Select(kv => new TagesberichtTaxBreakdownDto
            {
                TaxBucketKey = kv.Key,
                TaxAmount = kv.Value,
                NetHint = 0
            })
            .OrderByDescending(x => x.TaxAmount)
            .ToList();

        return new TagesberichtSummaryDto
        {
            SchemaVersion = SnapshotSchemaVersion,
            PeriodStartUtc = fromUtc,
            PeriodEndUtcExclusive = toExclusiveUtc,
            ViennaBusinessDate = viennaDate,
            CashRegisterId = cashRegisterId,
            RegisterNumber = register?.RegisterNumber,
            StoreLabel = register?.Location,
            OperatorUserIdScope = operatorScope,
            SalePaymentRowCount = saleLike.Count,
            RefundRowCount = refunds.Count,
            StornoRowCount = stornos.Count,
            GrossSalesAmount = gross,
            TaxTotalAmount = taxTotal,
            RefundAmountTotal = refundTotal,
            PaymentMethodBreakdown = methodBuckets,
            TaxBreakdown = taxBreakdown,
            Reconciliation = new TagesberichtReconciliationFlagsDto
            {
                PaymentsWithoutInvoiceCount = paymentsWithoutInvoice,
                UnknownPaymentMethodRowCount = unknownMethod,
                OfflineLinkedPaymentCount = offlineLinked,
                DayClosedInRksv = dayClosed,
                DailyClosingId = dayClosed ? dailyClosing?.Id : null
            },
            Warnings = warnings,
            TracePaymentDetailIds = traceIds,
            TracePaymentIdsHash = traceHash
        };
    }

    private static void MergeTaxDetails(decimal rowTax, JsonDocument taxDetails, Dictionary<string, decimal> buckets)
    {
        try
        {
            if (taxDetails.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in taxDetails.RootElement.EnumerateObject())
                {
                    if (p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetDecimal(out var d))
                    {
                        var key = string.IsNullOrEmpty(p.Name) ? "unnamed" : p.Name;
                        buckets[key] = buckets.GetValueOrDefault(key) + d;
                    }
                }
            }
        }
        catch
        {
            // ignore parse errors
        }

        buckets["row_tax_sum"] = buckets.GetValueOrDefault("row_tax_sum") + rowTax;
    }

    private static string? TryPaymentMethodLabel(string raw)
    {
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) &&
            Enum.IsDefined(typeof(PaymentMethod), n))
            return ((PaymentMethod)n).ToString();
        return null;
    }

    private async Task<string?> ResolveStoreLabelAsync(Guid cashRegisterId, CancellationToken cancellationToken)
    {
        var r = await _db.CashRegisters.AsNoTracking()
            .Where(x => x.Id == cashRegisterId)
            .Select(x => x.Location)
            .FirstOrDefaultAsync(cancellationToken);
        return r;
    }

    private async Task<TagesberichtSubmissionStateDto> BuildSubmissionStateAsync(
        TagesberichtReport row,
        CancellationToken cancellationToken)
    {
        var envelope = await _submissionCompat.BuildEnvelopeAsync(new BuildReportSubmissionEnvelopeRequest
        {
            ReportType = "Tagesbericht",
            ReportId = row.Id,
            ReportState = row.ReportStatus,
            OutboxMessageId = row.LastFinanzOnlineOutboxMessageId,
            SupersedesReportId = row.SupersedesReportId,
            SupersededByReportId = row.SupersededByReportId
        }, cancellationToken);
        return _submissionCompat.ToLegacySubmissionState(envelope);
    }

    private static string MapOutboxToLifecycle(string status)
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

    private static string? MapOperatorHintDe(string status, string? err)
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

    private async Task<TagesberichtDto?> MapToDtoAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await _db.Set<TagesberichtReport>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (row == null) return null;

        var summary = JsonSerializer.Deserialize<TagesberichtSummaryDto>(row.SnapshotJson, JsonOpts) ?? new TagesberichtSummaryDto();
        var reg = await _db.CashRegisters.AsNoTracking().FirstOrDefaultAsync(x => x.Id == row.CashRegisterId, cancellationToken);

        var envelope = await _submissionCompat.BuildEnvelopeAsync(new BuildReportSubmissionEnvelopeRequest
        {
            ReportType = "Tagesbericht",
            ReportId = row.Id,
            ReportState = row.ReportStatus,
            OutboxMessageId = row.LastFinanzOnlineOutboxMessageId,
            SupersedesReportId = row.SupersedesReportId,
            SupersededByReportId = row.SupersededByReportId
        }, cancellationToken);
        var submission = _submissionCompat.ToLegacySubmissionState(envelope);

        return new TagesberichtDto
        {
            Id = row.Id,
            ViennaBusinessDate = row.ViennaBusinessDate,
            CashRegisterId = row.CashRegisterId,
            RegisterNumber = reg?.RegisterNumber,
            StoreLabel = row.StoreLabel ?? reg?.Location,
            OperatorUserIdScope = row.OperatorUserIdScope,
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
            LinkedDailyClosingId = row.LinkedDailyClosingId,
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
                DescriptionDe = "Operative Ansicht; keine offizielle Buchhaltungs- oder Rechtsausgabe.",
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
                DescriptionDe = "Für Buchhaltung und Abstimmung; nicht als Rechtsnachweis verwenden.",
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

    private static DateTime NormalizeViennaDate(DateTime d)
    {
        return new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Unspecified);
    }

    private static string ComputeSnapshotHash(TagesberichtSummaryDto summary)
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
