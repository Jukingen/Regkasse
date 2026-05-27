using System.Globalization;
using System.Text;
using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services.Reports;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public interface IComplianceOperationalReportingService
{
    Task<DailyReconciliationReportDto> GetDailyReconciliationAsync(
        DateTime? businessDate,
        Guid? cashRegisterId,
        CancellationToken cancellationToken = default);

    Task<TseChainContinuityReportDto> GetTseChainContinuityAsync(
        DateTime? startDate,
        DateTime? endDate,
        Guid? cashRegisterId,
        CancellationToken cancellationToken = default);

    Task<(byte[] Content, string ContentType, string FileName)> ExportTseChainContinuityAsync(
        DateTime? startDate,
        DateTime? endDate,
        Guid? cashRegisterId,
        string format,
        CancellationToken cancellationToken = default);

    Task<OfflineRecoveryReportDto> GetOfflineRecoveryAsync(
        DateTime? startDate,
        DateTime? endDate,
        Guid? cashRegisterId,
        int recentLimit = 50,
        CancellationToken cancellationToken = default);

    Task<PeakHourHeatmapReportDto> GetPeakHourHeatmapAsync(
        DateTime? startDate,
        DateTime? endDate,
        Guid? cashRegisterId,
        CancellationToken cancellationToken = default);

    Task<ProductMovementReportDto> GetProductMovementAsync(
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default);
}

public sealed class ComplianceOperationalReportingService : IComplianceOperationalReportingService
{
    private readonly AppDbContext _db;
    private readonly IDailyClosingService _dailyClosing;
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly IPeakHoursAnalysisService _peakHours;
    private readonly IProductMovementAnalysisService _productMovement;

    public ComplianceOperationalReportingService(
        AppDbContext db,
        IDailyClosingService dailyClosing,
        ISettingsTenantResolver tenantResolver,
        IPeakHoursAnalysisService peakHours,
        IProductMovementAnalysisService productMovement)
    {
        _db = db;
        _dailyClosing = dailyClosing;
        _tenantResolver = tenantResolver;
        _peakHours = peakHours;
        _productMovement = productMovement;
    }

    public async Task<DailyReconciliationReportDto> GetDailyReconciliationAsync(
        DateTime? businessDate,
        Guid? cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        var day = businessDate ?? PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
        var (fromUtc, toExclusive) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(
            PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(day.Year, day.Month, day.Day));

        var summary = await _dailyClosing.GenerateClosingSummaryAsync(
            tenantId, cashRegisterId, day, cancellationToken);

        CashRegister? register = null;
        if (cashRegisterId.HasValue && cashRegisterId.Value != Guid.Empty)
        {
            register = await _db.CashRegisters.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == cashRegisterId.Value && r.TenantId == tenantId, cancellationToken);
        }

        decimal openingBalance = register?.StartingBalance ?? 0m;
        if (cashRegisterId.HasValue && cashRegisterId.Value != Guid.Empty)
        {
            var openTx = await _db.CashRegisterTransactions.AsNoTracking()
                .Where(t => t.CashRegisterId == cashRegisterId.Value
                            && t.TransactionType == TransactionType.Open
                            && t.TransactionDate >= fromUtc
                            && t.TransactionDate < toExclusive
                            && t.IsActive)
                .OrderBy(t => t.TransactionDate)
                .FirstOrDefaultAsync(cancellationToken);
            if (openTx != null)
                openingBalance = openTx.Amount;
        }

        var expectedCash = openingBalance + summary.TotalCash;

        decimal? actualCash = null;
        string? reconciledBy = null;
        DateTime? reconciledAt = null;
        if (cashRegisterId.HasValue && cashRegisterId.Value != Guid.Empty)
        {
            var closeTx = await _db.CashRegisterTransactions.AsNoTracking()
                .Where(t => t.CashRegisterId == cashRegisterId.Value
                            && t.TransactionType == TransactionType.Close
                            && t.TransactionDate >= fromUtc
                            && t.TransactionDate < toExclusive
                            && t.IsActive)
                .OrderByDescending(t => t.TransactionDate)
                .FirstOrDefaultAsync(cancellationToken);
            if (closeTx != null)
            {
                actualCash = closeTx.Amount;
                reconciledBy = closeTx.UserId;
                reconciledAt = closeTx.TransactionDate;
            }
        }

        decimal? cashDifference = actualCash.HasValue ? expectedCash - actualCash.Value : null;
        const decimal tolerance = 0.01m;
        var isReconciled = actualCash.HasValue && cashDifference.HasValue && Math.Abs(cashDifference.Value) <= tolerance;

        var (vIssued, vRedeemed, vExpired) = await CountVoucherLedgerAsync(tenantId, fromUtc, toExclusive, cancellationToken);

        return new DailyReconciliationReportDto
        {
            BusinessDate = summary.BusinessDate,
            CashRegisterId = cashRegisterId,
            RegisterNumber = register?.RegisterNumber,
            CashTotal = summary.TotalCash,
            CardTotal = summary.TotalCard,
            VoucherTotal = summary.TotalVoucherRedemptions,
            OtherTotal = summary.TotalOtherPaymentMethods,
            OpeningBalance = openingBalance,
            ExpectedCash = expectedCash,
            ActualCash = actualCash,
            CashDifference = cashDifference,
            VouchersIssued = vIssued,
            VouchersRedeemed = vRedeemed,
            VouchersExpired = vExpired,
            IsReconciled = isReconciled,
            ReconciledByUserId = reconciledBy,
            ReconciledAtUtc = reconciledAt,
            Notes = actualCash == null
                ? "Kein Schichtabschluss (Close) am gewählten Tag — Ist-Bargeld fehlt."
                : null,
            DisclaimerDe =
                "Soll-Bargeld = Eröffnungssaldo + Barumsatz (Zahlungszeilen). Ist-Bargeld aus Kassen-Schließung (Close-Transaktion), sofern vorhanden.",
        };
    }

    public async Task<TseChainContinuityReportDto> GetTseChainContinuityAsync(
        DateTime? startDate,
        DateTime? endDate,
        Guid? cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        var (fromUtc, endBoundUtc, endExclusive, repStart, repEnd) = ResolveRange(startDate, endDate);
        var registers = await BuildTseContinuityRegistersAsync(
            fromUtc, endBoundUtc, endExclusive, repStart, repEnd, cashRegisterId, cancellationToken);

        return new TseChainContinuityReportDto
        {
            Meta = BuildMeta(fromUtc, endBoundUtc, repStart, repEnd, cashRegisterId),
            Registers = registers,
            TotalReceiptsChecked = registers.Sum(r => r.ReceiptsInRange),
            TotalSignatureCount = registers.Sum(r => r.SignatureCount),
            TotalGapsCount = registers.Sum(r => r.GapsCount),
            TotalDuplicateCount = registers.Sum(r => r.DuplicateCount),
            BreakCount = registers.Sum(r => r.ChainBreakCount),
        };
    }

    public async Task<(byte[] Content, string ContentType, string FileName)> ExportTseChainContinuityAsync(
        DateTime? startDate,
        DateTime? endDate,
        Guid? cashRegisterId,
        string format,
        CancellationToken cancellationToken = default)
    {
        var (fromUtc, endBoundUtc, endExclusive, repStart, repEnd) = ResolveRange(startDate, endDate);
        var registerIds = await ResolveRegisterIdsAsync(cashRegisterId, cancellationToken);
        var detailRows = new List<TseChainDetailRowDto>();

        foreach (var regId in registerIds)
        {
            detailRows.AddRange(
                await LoadTseChainDetailRowsAsync(regId, fromUtc, endBoundUtc, endExclusive, cancellationToken));
        }

        var fmt = (format ?? "csv").Trim().ToLowerInvariant();
        var fileStem = $"tse-chain-{repStart:yyyyMMdd}-{repEnd:yyyyMMdd}";

        if (fmt == "json")
        {
            var json = JsonSerializer.Serialize(detailRows, new JsonSerializerOptions { WriteIndented = true });
            return (Encoding.UTF8.GetBytes(json), "application/json; charset=utf-8", $"{fileStem}.json");
        }

        var sb = new StringBuilder();
        sb.AppendLine("cashRegisterId,receiptId,receiptNumber,createdAtUtc,hasSignature,parsedSequence,parsedSequenceDateYmd,chainLinkValid,signaturePreview,prevSignaturePreview");
        var inv = CultureInfo.InvariantCulture;
        foreach (var row in detailRows)
        {
            sb.Append(row.CashRegisterId.ToString()).Append(',');
            sb.Append(row.ReceiptId.ToString()).Append(',');
            sb.Append(EscapeCsv(row.ReceiptNumber)).Append(',');
            sb.Append(row.CreatedAtUtc.ToString("O", inv)).Append(',');
            sb.Append(row.HasSignature ? "1" : "0").Append(',');
            sb.Append(row.ParsedSequence?.ToString(inv) ?? "").Append(',');
            sb.Append(EscapeCsv(row.ParsedSequenceDateYmd)).Append(',');
            sb.Append(row.ChainLinkValid ? "1" : "0").Append(',');
            sb.Append(EscapeCsv(row.SignaturePreview)).Append(',');
            sb.AppendLine(EscapeCsv(row.PrevSignaturePreview));
        }

        return (Encoding.UTF8.GetBytes(sb.ToString()), "text/csv; charset=utf-8", $"{fileStem}.csv");
    }

    private async Task<List<TseContinuityRegisterReportDto>> BuildTseContinuityRegistersAsync(
        DateTime fromUtc,
        DateTime endBoundUtc,
        bool endExclusive,
        DateTime repStart,
        DateTime repEnd,
        Guid? cashRegisterId,
        CancellationToken cancellationToken)
    {
        var registerIds = await ResolveRegisterIdsAsync(cashRegisterId, cancellationToken);
        var summaries = new List<TseContinuityRegisterReportDto>();

        foreach (var regId in registerIds)
        {
            var reg = await _db.CashRegisters.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == regId, cancellationToken);
            var chainState = await _db.SignatureChainState.AsNoTracking()
                .FirstOrDefaultAsync(s => s.CashRegisterId == regId, cancellationToken);

            var receiptsQuery = _db.Receipts.AsNoTracking().Where(r => r.CashRegisterId == regId);
            receiptsQuery = endExclusive
                ? receiptsQuery.Where(r => r.CreatedAt >= fromUtc && r.CreatedAt < endBoundUtc)
                : receiptsQuery.Where(r => r.CreatedAt >= fromUtc && r.CreatedAt <= endBoundUtc);

            var receipts = await receiptsQuery
                .OrderBy(r => r.CreatedAt)
                .Select(r => new
                {
                    r.ReceiptId,
                    r.ReceiptNumber,
                    r.CreatedAt,
                    r.SignatureValue,
                    r.PrevSignatureValue,
                })
                .ToListAsync(cancellationToken);

            var links = receipts.Select(r =>
            {
                var parsed = TseChainContinuityAnalyzer.TryParseBelegNrSequence(r.ReceiptNumber, out var seq, out var ymd);
                return new TseChainContinuityAnalyzer.ReceiptLink
                {
                    ReceiptId = r.ReceiptId,
                    ReceiptNumber = r.ReceiptNumber,
                    CreatedAtUtc = r.CreatedAt,
                    SignatureValue = r.SignatureValue,
                    PrevSignatureValue = r.PrevSignatureValue,
                    ParsedSequence = parsed ? seq : null,
                    ParsedSequenceDateYmd = ymd,
                };
            }).ToList();

            var report = TseChainContinuityAnalyzer.AnalyzeRegister(
                regId,
                reg?.RegisterNumber,
                repStart,
                repEnd,
                links,
                chainState?.LastCounter ?? 0);
            report.LastSignaturePreview = TruncateSig(chainState?.LastSignature);
            summaries.Add(report);
        }

        return summaries;
    }

    private async Task<IReadOnlyList<TseChainDetailRowDto>> LoadTseChainDetailRowsAsync(
        Guid regId,
        DateTime fromUtc,
        DateTime endBoundUtc,
        bool endExclusive,
        CancellationToken cancellationToken)
    {
        var receiptsQuery = _db.Receipts.AsNoTracking().Where(r => r.CashRegisterId == regId);
        receiptsQuery = endExclusive
            ? receiptsQuery.Where(r => r.CreatedAt >= fromUtc && r.CreatedAt < endBoundUtc)
            : receiptsQuery.Where(r => r.CreatedAt >= fromUtc && r.CreatedAt <= endBoundUtc);

        var receipts = await receiptsQuery
            .OrderBy(r => r.CreatedAt)
            .Select(r => new
            {
                r.ReceiptId,
                r.ReceiptNumber,
                r.CreatedAt,
                r.SignatureValue,
                r.PrevSignatureValue,
            })
            .ToListAsync(cancellationToken);

        var rows = new List<TseChainDetailRowDto>();
        string? previousSig = null;
        foreach (var r in receipts)
        {
            TseChainContinuityAnalyzer.TryParseBelegNrSequence(r.ReceiptNumber, out var seq, out var ymd);
            var hasSig = !string.IsNullOrWhiteSpace(r.SignatureValue);
            var actualPrev = r.PrevSignatureValue ?? string.Empty;
            var chainOk = previousSig == null
                || !hasSig
                || string.Equals(actualPrev, previousSig, StringComparison.Ordinal);

            rows.Add(new TseChainDetailRowDto
            {
                CashRegisterId = regId,
                ReceiptId = r.ReceiptId,
                ReceiptNumber = r.ReceiptNumber,
                CreatedAtUtc = r.CreatedAt,
                HasSignature = hasSig,
                ParsedSequence = seq > 0 ? seq : null,
                ParsedSequenceDateYmd = ymd,
                ChainLinkValid = chainOk,
                SignaturePreview = TruncateSig(r.SignatureValue),
                PrevSignaturePreview = TruncateSig(r.PrevSignatureValue),
            });

            if (hasSig)
                previousSig = r.SignatureValue;
        }

        return rows;
    }

    private static string EscapeCsv(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "\"\"";
        return "\"" + s.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    public async Task<OfflineRecoveryReportDto> GetOfflineRecoveryAsync(
        DateTime? startDate,
        DateTime? endDate,
        Guid? cashRegisterId,
        int recentLimit = 50,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        var (fromUtc, endBoundUtc, endExclusive, repStart, repEnd) = ResolveRange(startDate, endDate);

        var q = _db.OfflineTransactions.AsNoTracking().Where(x => x.TenantId == tenantId);
        if (cashRegisterId.HasValue && cashRegisterId.Value != Guid.Empty)
            q = q.Where(x => x.CashRegisterId == cashRegisterId.Value);

        q = endExclusive
            ? q.Where(x => x.ServerReceivedAtUtc < endBoundUtc)
            : q.Where(x => x.ServerReceivedAtUtc <= endBoundUtc);

        var cohort = await q.ToListAsync(cancellationToken);

        var registerIds = cohort.Select(x => x.CashRegisterId).Distinct().ToList();
        var registerNumbers = await _db.CashRegisters.AsNoTracking()
            .Where(r => registerIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.RegisterNumber, cancellationToken);

        return OfflineRecoveryMetricsCalculator.Build(
            repStart,
            repEnd,
            fromUtc,
            endBoundUtc,
            endExclusive,
            cohort,
            registerNumbers,
            recentLimit);
    }

    public async Task<PeakHourHeatmapReportDto> GetPeakHourHeatmapAsync(
        DateTime? startDate,
        DateTime? endDate,
        Guid? cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        var peak = await _peakHours.GetPeakHoursAsync(startDate, endDate, cashRegisterId, cancellationToken);
        var dayTotals = new List<PeakHourDayTotalDto>();
        for (var d = 0; d < 7; d++)
        {
            dayTotals.Add(new PeakHourDayTotalDto
            {
                DayOfWeek = d,
                Count = peak.Heatmap[d].Sum(),
                Amount = 0,
            });
        }

        return new PeakHourHeatmapReportDto
        {
            Meta = new OperationalReportMetaDto
            {
                SchemaVersion = "2.0-peak-hours",
                ReportGeneratedAtUtc = DateTime.UtcNow,
                PeriodStartLocalDate = peak.PeriodStartLocal,
                PeriodEndLocalDate = peak.PeriodEndLocal,
                CashRegisterId = cashRegisterId,
            },
            Cells = peak.Heatmap,
            MaxCellCount = peak.MaxCellCount,
            MaxCellAmount = 0,
            DayTotals = dayTotals,
            BusiestHour = peak.BusiestHour,
            QuietestHour = peak.QuietestHour,
            AverageTransactionsPerHour = peak.AverageTransactionsPerHour,
            RecommendedStaffingLevels = peak.RecommendedStaffingLevels,
        };
    }

    public Task<ProductMovementReportDto> GetProductMovementAsync(
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default) =>
        _productMovement.GetProductMovementAsync(startDate, endDate, cancellationToken);

    private async Task<(int Issued, int Redeemed, int Expired)> CountVoucherLedgerAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toExclusive,
        CancellationToken cancellationToken)
    {
        var entries = await _db.VoucherLedgerEntries.AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.CreatedAtUtc >= fromUtc && e.CreatedAtUtc < toExclusive)
            .Select(e => e.Type)
            .ToListAsync(cancellationToken);

        return (
            entries.Count(t => t == VoucherTransactionType.Issue),
            entries.Count(t => t == VoucherTransactionType.Redeem),
            entries.Count(t => t == VoucherTransactionType.Expire));
    }

    private async Task<List<Guid>> ResolveRegisterIdsAsync(Guid? cashRegisterId, CancellationToken cancellationToken)
    {
        if (cashRegisterId.HasValue && cashRegisterId.Value != Guid.Empty)
            return [cashRegisterId.Value];

        return await _db.CashRegisters.AsNoTracking()
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);
    }

    private static OperationalReportMetaDto BuildMeta(
        DateTime fromUtc,
        DateTime endBoundUtc,
        DateTime repStart,
        DateTime repEnd,
        Guid? cashRegisterId) =>
        new()
        {
            SchemaVersion = "1.0",
            ReportGeneratedAtUtc = DateTime.UtcNow,
            PeriodStartUtc = fromUtc,
            PeriodEndUtc = endBoundUtc,
            PeriodStartLocalDate = repStart,
            PeriodEndLocalDate = repEnd,
            CashRegisterId = cashRegisterId,
        };

    private static string? TruncateSig(string? sig)
    {
        if (string.IsNullOrEmpty(sig)) return sig;
        return sig.Length <= 48 ? sig : sig[..48] + "…";
    }

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
