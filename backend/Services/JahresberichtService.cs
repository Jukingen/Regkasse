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

public sealed class JahresberichtService : IJahresberichtService
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

    public JahresberichtService(
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

    public async Task<JahresberichtDto> GenerateOrRefreshProvisionalAsync(
        JahresberichtGenerationRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            await AuditReportMutationFailureAsync("JahresberichtGenerateFailed", actorUserId, "Jahresbericht generation failed", "Actor required.", null, new { request.ViennaYearAnyDay, request.ScopeKind, request.CashRegisterId, request.ForceNewProvisional });
            throw new ArgumentException("Actor required.", nameof(actorUserId));
        }

        var yearStart = NormalizeViennaYearStart(request.ViennaYearAnyDay);
        var scopeKind = (request.ScopeKind ?? MonatsberichtScopeKinds.Register).Trim();
        if (scopeKind != MonatsberichtScopeKinds.Register && scopeKind != MonatsberichtScopeKinds.Company)
            throw new InvalidOperationException("Invalid ScopeKind. Use Register or Company.");

        if (scopeKind == MonatsberichtScopeKinds.Register && !request.CashRegisterId.HasValue)
            throw new InvalidOperationException("CashRegisterId required for Register scope.");

        if (scopeKind == MonatsberichtScopeKinds.Company && request.CashRegisterId.HasValue)
            throw new InvalidOperationException("CashRegisterId must be null for Company scope.");

        if (scopeKind == MonatsberichtScopeKinds.Register &&
            !await _db.CashRegisters.AsNoTracking().AnyAsync(x => x.Id == request.CashRegisterId!.Value, cancellationToken))
        {
            await AuditReportMutationFailureAsync("JahresberichtGenerateFailed", actorUserId, "Jahresbericht generation failed", "Cash register not found.", null, new { request.ViennaYearAnyDay, request.ScopeKind, request.CashRegisterId, request.ForceNewProvisional });
            throw new InvalidOperationException("Cash register not found.");
        }

        var existing = await _db.Set<JahresberichtReport>()
            .Where(x =>
                x.ViennaYearStart == yearStart &&
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
            var built = await BuildYearlySnapshotAsync(yearStart, scopeKind, request.CashRegisterId, cancellationToken);
            existing.SnapshotJson = JsonSerializer.Serialize(built, JsonOpts);
            existing.SnapshotHash = ComputeSnapshotHash(built);
            existing.SnapshotSchemaVersion = SnapshotSchemaVersion;
            existing.SnapshotGrossSalesAmount = built.AggregationFromMonthly.GrossSalesAmount;
            existing.StoreLabel = await ResolveStoreLabelAsync(scopeKind, request.CashRegisterId, cancellationToken);
            FormalReportPropagationMarkers.ClearUpstreamReview(existing);
            await _db.SaveChangesAsync(cancellationToken);
            await AuditReportMutationSuccessAsync("JahresberichtSnapshotRefreshed", actorUserId, "Jahresbericht provisional refreshed", existing, new { yearStart, scopeKind, request.CashRegisterId, request.ForceNewProvisional }, new { existing.SnapshotHash });
            return await MapToDtoAsync(existing.Id, cancellationToken) ?? throw new InvalidOperationException("Map failed.");
        }

        var summary = await BuildYearlySnapshotAsync(yearStart, scopeKind, request.CashRegisterId, cancellationToken);

        var row = new JahresberichtReport
        {
            ViennaYearStart = yearStart,
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
            SnapshotGrossSalesAmount = summary.AggregationFromMonthly.GrossSalesAmount,
        };
        row.OriginalReportId = row.Id;

        _db.Set<JahresberichtReport>().Add(row);
        await _db.SaveChangesAsync(cancellationToken);

        await AuditReportMutationSuccessAsync("JahresberichtGenerated", actorUserId, "Jahresbericht provisional created", row, new { yearStart, scopeKind, request.CashRegisterId, request.ForceNewProvisional }, new { row.SnapshotHash });

        return await MapToDtoAsync(row.Id, cancellationToken) ?? throw new InvalidOperationException("Map failed.");
    }

    public async Task<JahresberichtDto> FinalizeAsync(JahresberichtFinalizeRequest request, string actorUserId, CancellationToken cancellationToken = default)
    {
        var row = await _db.Set<JahresberichtReport>().FirstOrDefaultAsync(x => x.Id == request.ReportId, cancellationToken)
            ?? throw new InvalidOperationException("Jahresbericht not found.");

        if (row.ReportStatus == MonatsberichtReportStatuses.Finalized)
        {
            await AuditReportMutationFailureAsync("JahresberichtFinalizeFailed", actorUserId, "Jahresbericht finalize failed", "Report already finalized.", row, new { request.ReportId, request.Note });
            throw new InvalidOperationException("Report already finalized.");
        }

        if (row.ReportStatus == MonatsberichtReportStatuses.Superseded)
        {
            await AuditReportMutationFailureAsync("JahresberichtFinalizeFailed", actorUserId, "Jahresbericht finalize failed", "Cannot finalize superseded report.", row, new { request.ReportId, request.Note });
            throw new InvalidOperationException("Cannot finalize superseded report.");
        }

        var summary = await BuildYearlySnapshotAsync(row.ViennaYearStart, row.ScopeKind, row.CashRegisterId, cancellationToken);

        row.SnapshotJson = JsonSerializer.Serialize(summary, JsonOpts);
        row.SnapshotHash = ComputeSnapshotHash(summary);
        row.SnapshotSchemaVersion = SnapshotSchemaVersion;
        row.ReportStatus = MonatsberichtReportStatuses.Finalized;
        row.FinalizedAtUtc = DateTime.UtcNow;
        row.FinalizedByUserId = actorUserId;
        row.ReportRevisionReason = request.Note ?? row.ReportRevisionReason ?? "Finalized";
        row.SnapshotGrossSalesAmount = summary.AggregationFromMonthly.GrossSalesAmount;
        FormalReportPropagationMarkers.ClearUpstreamReview(row);

        await _db.SaveChangesAsync(cancellationToken);

        await AuditReportMutationSuccessAsync("JahresberichtFinalized", actorUserId, request.Note ?? "Jahresbericht finalized", row, new { request.ReportId, request.Note }, new { row.SnapshotHash });

        return await MapToDtoAsync(row.Id, cancellationToken) ?? throw new InvalidOperationException("Map failed.");
    }

    public async Task<JahresberichtDto> CreateCorrectionAsync(JahresberichtCorrectionRequest request, string actorUserId, CancellationToken cancellationToken = default)
    {
        var prior = await _db.Set<JahresberichtReport>().FirstOrDefaultAsync(x => x.Id == request.SupersedesReportId, cancellationToken)
            ?? throw new InvalidOperationException("Prior Jahresbericht not found.");

        if (prior.ReportStatus != MonatsberichtReportStatuses.Finalized)
        {
            await AuditReportMutationFailureAsync("JahresberichtCorrectionFailed", actorUserId, "Jahresbericht correction failed", "Correction requires finalized prior report.", prior, new { request.SupersedesReportId, request.Reason });
            throw new InvalidOperationException("Correction requires finalized prior report.");
        }

        if (prior.SupersededByReportId != null)
        {
            await AuditReportMutationFailureAsync("JahresberichtCorrectionFailed", actorUserId, "Jahresbericht correction failed", "Prior already superseded.", prior, new { request.SupersedesReportId, request.Reason });
            throw new InvalidOperationException("Prior already superseded.");
        }

        var duplicate = await _db.Set<JahresberichtReport>().AsNoTracking()
            .AnyAsync(x => x.CorrectionOfReportId == prior.Id && x.SupersededByReportId == null, cancellationToken);
        if (duplicate)
        {
            await AuditReportMutationFailureAsync("JahresberichtCorrectionFailed", actorUserId, "Jahresbericht correction failed", "Duplicate correction request blocked for same prior report.", prior, new { request.SupersedesReportId, request.Reason });
            throw new InvalidOperationException("Duplicate correction request blocked for same prior report.");
        }

        var summary = await BuildYearlySnapshotAsync(prior.ViennaYearStart, prior.ScopeKind, prior.CashRegisterId, cancellationToken);

        var row = new JahresberichtReport
        {
            ViennaYearStart = prior.ViennaYearStart,
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
            SnapshotGrossSalesAmount = summary.AggregationFromMonthly.GrossSalesAmount,
        };

        prior.SupersededByReportId = row.Id;
        prior.ReportStatus = MonatsberichtReportStatuses.Superseded;

        _db.Set<JahresberichtReport>().Add(row);
        await _db.SaveChangesAsync(cancellationToken);

        await AuditReportMutationSuccessAsync("JahresberichtCorrectionCreated", actorUserId, request.Reason ?? "Jahresbericht correction", row, new { request.SupersedesReportId, request.Reason, supersededReportId = prior.Id }, new { correctionReportId = row.Id, row.SubmissionImpact, row.CorrectionType });

        return await MapToDtoAsync(row.Id, cancellationToken) ?? throw new InvalidOperationException("Map failed.");
    }

    public async Task<JahresberichtDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await MapToDtoAsync(id, cancellationToken);

    public async Task<IReadOnlyList<JahresberichtListItemDto>> ListAsync(
        DateTime? fromYear,
        DateTime? toYear,
        string? scopeKind,
        Guid? cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        var q = _db.Set<JahresberichtReport>().AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(scopeKind))
            q = q.Where(x => x.ScopeKind == scopeKind.Trim());

        if (cashRegisterId.HasValue)
            q = q.Where(x => x.CashRegisterId == cashRegisterId.Value);

        if (fromYear.HasValue)
        {
            var lo = NormalizeViennaYearStart(fromYear.Value);
            q = q.Where(x => x.ViennaYearStart >= lo);
        }

        if (toYear.HasValue)
        {
            var hi = NormalizeViennaYearStart(toYear.Value);
            q = q.Where(x => x.ViennaYearStart <= hi);
        }

        var rows = await q
            .OrderByDescending(x => x.ViennaYearStart)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);

        var regIds = rows.Where(x => x.CashRegisterId.HasValue).Select(x => x.CashRegisterId!.Value).Distinct().ToList();
        var regs = await _db.CashRegisters.AsNoTracking()
            .Where(r => regIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.RegisterNumber, cancellationToken);

        var list = new List<JahresberichtListItemDto>();
        foreach (var x in rows)
        {
            string? regNo = null;
            if (x.CashRegisterId.HasValue)
                regs.TryGetValue(x.CashRegisterId.Value, out regNo);

            list.Add(new JahresberichtListItemDto
            {
                Id = x.Id,
                ViennaYearStart = x.ViennaYearStart,
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
                UpstreamReviewRequired = x.UpstreamReviewRequired,
                UpstreamReviewReasonCode = x.UpstreamReviewReasonCode,
            });
        }

        return list;
    }

    public async Task<JahresberichtDto> SubmitToFinanzOnlineAsync(Guid reportId, string actorUserId, CancellationToken cancellationToken = default)
    {
        JahresberichtReport? row = null;
        try
        {
        row = await _db.Set<JahresberichtReport>().FirstOrDefaultAsync(x => x.Id == reportId, cancellationToken)
            ?? throw new InvalidOperationException("Jahresbericht not found.");

        if (row.ReportStatus != MonatsberichtReportStatuses.Finalized)
        {
            await AuditReportMutationFailureAsync("JahresberichtFinanzOnlineSubmitFailed", actorUserId, "Jahresbericht outbox enqueue failed", "Only finalized Jahresbericht can be submitted.", row, new { reportId });
            throw new InvalidOperationException("Only finalized Jahresbericht can be submitted.");
        }

        var pre = await FormalReportSubmissionGuards.EvaluateSubmitPrecheckAsync(
            _db,
            "JahresberichtReport",
            row.Id,
            row.ReportStatus,
            row.SupersededByReportId,
            row.LastFinanzOnlineOutboxMessageId,
            cancellationToken);
        if (pre == FormalReportSubmitPrecheckDecision.RejectSuperseded)
        {
            await AuditReportMutationFailureAsync("JahresberichtFinanzOnlineSubmitFailed", actorUserId, "Jahresbericht outbox enqueue failed", "Superseded Jahresbericht cannot be submitted.", row, new { reportId });
            throw new InvalidOperationException("Superseded Jahresbericht cannot be submitted. Use the successor report.");
        }
        if (pre == FormalReportSubmitPrecheckDecision.RejectAlreadyAccepted)
        {
            await AuditReportMutationFailureAsync("JahresberichtFinanzOnlineSubmitFailed", actorUserId, "Jahresbericht outbox enqueue failed", "Submission already accepted.", row, new { reportId });
            throw new InvalidOperationException(
                "FinanzOnline submission already accepted for this report. Create a correction report for a new submission chain.");
        }
        if (pre == FormalReportSubmitPrecheckDecision.ReturnExistingWithoutEnqueue)
            return await MapToDtoAsync(row.Id, cancellationToken) ?? throw new InvalidOperationException("Map failed.");

        var summary = JsonSerializer.Deserialize<JahresberichtSummaryDto>(row.SnapshotJson, JsonOpts)
            ?? throw new InvalidOperationException("Snapshot corrupt.");

        var registerNumber = "COMPANY";
        if (row.CashRegisterId.HasValue)
        {
            var reg = await _db.CashRegisters.AsNoTracking().FirstAsync(x => x.Id == row.CashRegisterId.Value, cancellationToken);
            registerNumber = reg.RegisterNumber;
        }

        var mode = FinanzOnlineIntegrationMode.TEST;

        var submissionAttemptIndex = await FormalReportSubmissionGuards.CountOutboxMessagesForAggregateAsync(
            _db, "JahresberichtReport", row.Id, cancellationToken);

        var payloadJson = JsonSerializer.Serialize(new
        {
            kind = "JahresberichtAnnualSummary",
            reportId = row.Id,
            snapshotHash = row.SnapshotHash,
            schemaVersion = row.SnapshotSchemaVersion,
            viennaYear = summary.ViennaYear,
            scopeKind = row.ScopeKind,
            registerNumber,
            grossFromMonthly = summary.AggregationFromMonthly.GrossSalesAmount,
            grossFromRaw = summary.RawPaymentRollup.GrossSalesAmount,
            submissionAttemptIndex,
        }, JsonOpts);

        var hashHex = ComputeSha256Hex(payloadJson);
        var businessKey = ReportFinanzOnlineBusinessKeys.Jahresbericht(
            row.ScopeKind,
            summary.ViennaYear,
            registerNumber,
            row.Id,
            submissionAttemptIndex);

        var previousOutboxId = row.LastFinanzOnlineOutboxMessageId;
        var previousStatus = row.LastSubmissionStatusCode;
        var msg = await _outbox.EnqueueSubmissionAsync(
            aggregateType: "JahresberichtReport",
            aggregateId: row.Id,
            messageType: FinanzOnlineJahresberichtMessageTypes.JahresberichtAnnualSummary,
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

        await AuditReportMutationSuccessAsync(
            "JahresberichtFinanzOnlineSubmit",
            actorUserId,
            "Jahresbericht enqueued to FinanzOnline outbox",
            row,
            new { reportId = row.Id, previousOutboxId, previousStatus, retrySubmissionAttempt = previousOutboxId != null },
            new { outboxMessageId = msg.Id, outboxStatus = msg.Status, correlationId = msg.CorrelationId, businessKey = msg.BusinessKey },
            msg.CorrelationId);

        return await MapToDtoAsync(row.Id, cancellationToken) ?? throw new InvalidOperationException("Map failed.");
        }
        catch (Exception ex)
        {
            await AuditReportMutationFailureAsync("JahresberichtFinanzOnlineSubmitFailed", actorUserId, "Jahresbericht outbox enqueue failed", ex.Message, row, new { reportId, retrySubmissionAttempt = row?.LastFinanzOnlineOutboxMessageId != null });
            throw;
        }
    }

    private Task AuditReportMutationSuccessAsync(
        string action,
        string actorUserId,
        string description,
        JahresberichtReport row,
        object? requestData,
        object? responseData,
        string? correlationIdOverride = null)
    {
        return _audit.LogSystemOperationAsync(
            action,
            nameof(JahresberichtReport),
            actorUserId,
            "ReportActor",
            description: description,
            status: AuditLogStatus.Success,
            requestData: new
            {
                reportType = "Jahresbericht",
                reportId = row.Id,
                reportVersion = row.ReportVersion,
                reportStatus = row.ReportStatus,
                scopeKind = row.ScopeKind,
                scopeId = row.CashRegisterId,
                period = row.ViennaYearStart.ToString("yyyy", CultureInfo.InvariantCulture),
                originalReportId = row.OriginalReportId,
                correctionOfReportId = row.CorrectionOfReportId,
                supersedesReportId = row.SupersedesReportId,
                supersededByReportId = row.SupersededByReportId,
                outboxMessageId = row.LastFinanzOnlineOutboxMessageId,
                actorUserId,
                requestData
            },
            responseData: responseData,
            correlationIdOverride: correlationIdOverride);
    }

    private Task AuditReportMutationFailureAsync(
        string action,
        string actorUserId,
        string description,
        string errorMessage,
        JahresberichtReport? row,
        object? requestData,
        string? correlationIdOverride = null)
    {
        return _audit.LogSystemOperationAsync(
            action,
            nameof(JahresberichtReport),
            actorUserId,
            "ReportActor",
            description: description,
            status: AuditLogStatus.Failed,
            errorDetails: errorMessage,
            requestData: new
            {
                reportType = "Jahresbericht",
                reportId = row?.Id,
                reportVersion = row?.ReportVersion,
                reportStatus = row?.ReportStatus,
                scopeKind = row?.ScopeKind,
                scopeId = row?.CashRegisterId,
                period = row == null ? null : row.ViennaYearStart.ToString("yyyy", CultureInfo.InvariantCulture),
                originalReportId = row?.OriginalReportId,
                correctionOfReportId = row?.CorrectionOfReportId,
                supersedesReportId = row?.SupersedesReportId,
                supersededByReportId = row?.SupersededByReportId,
                outboxMessageId = row?.LastFinanzOnlineOutboxMessageId,
                actorUserId,
                requestData
            },
            responseData: new { error = errorMessage },
            correlationIdOverride: correlationIdOverride);
    }

    private async Task<JahresberichtSummaryDto> BuildYearlySnapshotAsync(
        DateTime yearStart,
        string scopeKind,
        Guid? cashRegisterId,
        CancellationToken cancellationToken)
    {
        var yearEnd = yearStart.AddYears(1).AddDays(-1);
        var (fromUtc, toExclusiveUtc) = PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(yearStart, yearEnd);

        IQueryable<MonatsberichtReport> monthlyQ = _db.Set<MonatsberichtReport>().AsNoTracking()
            .Where(x =>
                x.ReportStatus == MonatsberichtReportStatuses.Finalized &&
                x.SupersededByReportId == null &&
                x.ViennaMonthStart >= yearStart &&
                x.ViennaMonthStart <= yearEnd);

        if (scopeKind == MonatsberichtScopeKinds.Register)
            monthlyQ = monthlyQ.Where(x => x.CashRegisterId == cashRegisterId!.Value);
        else
            monthlyQ = monthlyQ.Where(x => x.CashRegisterId == null);

        var monthlyRows = await monthlyQ
            .OrderBy(x => x.ViennaMonthStart)
            .ToListAsync(cancellationToken);

        var linked = new List<LinkedMonatsberichtLineDto>();
        var taxMerge = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var methodMerge = new Dictionary<string, (int Count, decimal Total)>(StringComparer.Ordinal);

        decimal sumGross = 0, sumTax = 0, sumRefund = 0;
        int sumSaleRows = 0, sumRefRows = 0, sumStorno = 0;

        foreach (var m in monthlyRows)
        {
            var snap = JsonSerializer.Deserialize<MonatsberichtSummaryDto>(m.SnapshotJson, JsonOpts);
            if (snap == null) continue;

            sumGross += snap.AggregationFromDaily.GrossSalesAmount;
            sumTax += snap.AggregationFromDaily.TaxTotalAmount;
            sumRefund += snap.AggregationFromDaily.RefundAmountTotal;
            sumSaleRows += snap.AggregationFromDaily.SalePaymentRowCount;
            sumRefRows += snap.AggregationFromDaily.RefundRowCount;
            sumStorno += snap.AggregationFromDaily.StornoRowCount;

            string? regNo = null;
            if (m.CashRegisterId.HasValue)
            {
                var reg = await _db.CashRegisters.AsNoTracking().FirstOrDefaultAsync(x => x.Id == m.CashRegisterId.Value, cancellationToken);
                regNo = reg?.RegisterNumber;
            }

            linked.Add(new LinkedMonatsberichtLineDto
            {
                MonatsberichtId = m.Id,
                ViennaMonthStart = m.ViennaMonthStart,
                CashRegisterId = m.CashRegisterId,
                RegisterNumber = regNo,
                SnapshotHash = m.SnapshotHash,
                GrossSalesAmount = snap.AggregationFromDaily.GrossSalesAmount,
                ReportStatus = m.ReportStatus
            });

            foreach (var t in snap.TaxBreakdown)
                taxMerge[t.TaxBucketKey] = taxMerge.GetValueOrDefault(t.TaxBucketKey) + t.TaxAmount;

            foreach (var p in snap.PaymentMethodBreakdown)
            {
                var cur = methodMerge.GetValueOrDefault(p.MethodKey);
                methodMerge[p.MethodKey] = (cur.Count + p.RowCount, cur.Total + p.TotalAmount);
            }
        }

        var distinctMonths = monthlyRows
            .Select(x => new DateTime(x.ViennaMonthStart.Year, x.ViennaMonthStart.Month, 1))
            .Distinct()
            .Count();

        var raw = await RollupRawPaymentsAsync(scopeKind, cashRegisterId, fromUtc, toExclusiveUtc, cancellationToken);
        var deltaGross = sumGross - raw.GrossSalesAmount;
        var adjustment = new JahresberichtAdjustmentDto
        {
            GrossDeltaMonthlyVsRaw = deltaGross,
            RequiresReview = Math.Abs(deltaGross) > 0.05m,
            NoteDe = Math.Abs(deltaGross) > 0.05m
                ? "Summe der Monatsberichte weicht von Rohzahlungen ab — Abstimmung prüfen."
                : null
        };

        var warnings = new List<string>
        {
            "aggregation:sum_of_finalized_monatsberichte",
            "reconciliation:raw_payment_details_year_range"
        };

        if (distinctMonths < 12)
            warnings.Add($"missing_calendar_months:{12 - distinctMonths}");
        if (monthlyRows.Count == 0)
            warnings.Add("no_linked_monthly_reports");
        if (Math.Abs(deltaGross) > 0.05m)
            warnings.Add("monthly_aggregate_vs_raw_payment_mismatch");

        var viennaNow = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
        if (yearStart.Year == viennaNow.Year)
            warnings.Add("provisional_current_year_incomplete");

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

        string? regNumber = null;
        if (cashRegisterId.HasValue)
        {
            var reg = await _db.CashRegisters.AsNoTracking().FirstOrDefaultAsync(x => x.Id == cashRegisterId.Value, cancellationToken);
            regNumber = reg?.RegisterNumber;
        }

        return new JahresberichtSummaryDto
        {
            SchemaVersion = SnapshotSchemaVersion,
            ViennaYear = yearStart.Year,
            PeriodStartUtc = fromUtc,
            PeriodEndUtcExclusive = toExclusiveUtc,
            ScopeKind = scopeKind,
            CashRegisterId = cashRegisterId,
            RegisterNumber = regNumber,
            StoreLabel = await ResolveStoreLabelAsync(scopeKind, cashRegisterId, cancellationToken),
            LinkedFinalizedMonatsberichte = linked,
            AggregationFromMonthly = new JahresberichtAggregationFromMonthlyDto
            {
                LinkedMonthlyReportCount = monthlyRows.Count,
                ExpectedMonthsInYear = 12,
                DistinctMonthsCovered = distinctMonths,
                GrossSalesAmount = sumGross,
                TaxTotalAmount = sumTax,
                RefundAmountTotal = sumRefund,
                SalePaymentRowCount = sumSaleRows,
                RefundRowCount = sumRefRows,
                StornoRowCount = sumStorno
            },
            RawPaymentRollup = raw,
            Adjustment = adjustment,
            PaymentMethodBreakdown = paymentBreakdown,
            TaxBreakdown = taxBreakdown,
            Warnings = warnings
        };
    }

    private async Task<JahresberichtRawPaymentRollupDto> RollupRawPaymentsAsync(
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

        return new JahresberichtRawPaymentRollupDto
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
        JahresberichtReport row,
        CancellationToken cancellationToken)
    {
        var envelope = await _submissionCompat.BuildEnvelopeAsync(new BuildReportSubmissionEnvelopeRequest
        {
            ReportType = "Jahresbericht",
            ReportId = row.Id,
            ReportState = row.ReportStatus,
            OutboxMessageId = row.LastFinanzOnlineOutboxMessageId,
            SupersedesReportId = row.SupersedesReportId,
            SupersededByReportId = row.SupersededByReportId
        }, cancellationToken);
        return _submissionCompat.ToLegacySubmissionState(envelope);
    }

    private async Task<JahresberichtDto?> MapToDtoAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await _db.Set<JahresberichtReport>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (row == null) return null;

        var summary = JsonSerializer.Deserialize<JahresberichtSummaryDto>(row.SnapshotJson, JsonOpts) ?? new JahresberichtSummaryDto();
        string? regNo = null;
        if (row.CashRegisterId.HasValue)
        {
            var reg = await _db.CashRegisters.AsNoTracking().FirstOrDefaultAsync(x => x.Id == row.CashRegisterId.Value, cancellationToken);
            regNo = reg?.RegisterNumber;
        }

        var envelope = await _submissionCompat.BuildEnvelopeAsync(new BuildReportSubmissionEnvelopeRequest
        {
            ReportType = "Jahresbericht",
            ReportId = row.Id,
            ReportState = row.ReportStatus,
            OutboxMessageId = row.LastFinanzOnlineOutboxMessageId,
            SupersedesReportId = row.SupersedesReportId,
            SupersededByReportId = row.SupersededByReportId
        }, cancellationToken);
        var submission = _submissionCompat.ToLegacySubmissionState(envelope);

        return new JahresberichtDto
        {
            Id = row.Id,
            ViennaYearStart = row.ViennaYearStart,
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
            ExportProfiles = BuildExportProfiles(),
            UpstreamPropagation = FormalReportPropagationNotes.ToUpstreamPropagationDto(row)
        };
    }

    private static IReadOnlyList<TagesberichtExportProfileDto> BuildExportProfiles() =>
        new[]
        {
            new TagesberichtExportProfileDto
            {
                ProfileKey = "operationalPreview",
                LabelDe = "Operational Preview",
                DescriptionDe = "Operative Jahresansicht; keine offizielle Buchhaltungs- oder Rechtsausgabe.",
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
                DescriptionDe = "Für Buchhaltung und Jahresabgleich; nicht als Rechtsnachweis verwenden.",
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

    private static DateTime NormalizeViennaYearStart(DateTime d) =>
        new(d.Year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    private static string ComputeSnapshotHash(JahresberichtSummaryDto summary)
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
