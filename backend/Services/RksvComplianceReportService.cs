using System.Globalization;
using System.Text.RegularExpressions;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <summary>
/// Builds the diagnostic RKSV compliance report. Performs all five checks in one DB read pass:
/// special receipts listing, signature chain continuity, BelegNr sequence gap detection,
/// TSE signature presence audit, and RKSV QR payload format validation.
/// </summary>
public sealed class RksvComplianceReportService : IRksvComplianceReportService
{
    private static readonly Regex ReceiptNumberRegex = new(
        @"^AT-(?<register>.+)-(?<ymd>\d{8})-(?<seq>\d+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly AppDbContext _db;
    private readonly IRksvReceiptQrPayloadFormatValidator _qrValidator;
    private readonly ILogger<RksvComplianceReportService> _logger;

    public RksvComplianceReportService(
        AppDbContext db,
        IRksvReceiptQrPayloadFormatValidator qrValidator,
        ILogger<RksvComplianceReportService> logger)
    {
        _db = db;
        _qrValidator = qrValidator;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<RksvComplianceReportDto> BuildReportAsync(
        Guid? cashRegisterId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default)
    {
        // Compose the receipts query (NoTracking, read-only).
        var receiptsQuery = _db.Receipts.AsNoTracking().AsQueryable();
        if (cashRegisterId.HasValue && cashRegisterId.Value != Guid.Empty)
            receiptsQuery = receiptsQuery.Where(r => r.CashRegisterId == cashRegisterId.Value);
        if (fromUtc.HasValue)
            receiptsQuery = receiptsQuery.Where(r => r.IssuedAt >= fromUtc.Value);
        if (toUtc.HasValue)
            receiptsQuery = receiptsQuery.Where(r => r.IssuedAt < toUtc.Value);

        var receipts = await receiptsQuery
            .OrderBy(r => r.CashRegisterId)
            .ThenBy(r => r.IssuedAt)
            .ThenBy(r => r.ReceiptNumber)
            .Select(r => new ReceiptProjection
            {
                ReceiptId = r.ReceiptId,
                PaymentId = r.PaymentId,
                ReceiptNumber = r.ReceiptNumber,
                CashRegisterId = r.CashRegisterId,
                IssuedAt = r.IssuedAt,
                SignatureValue = r.SignatureValue,
                PrevSignatureValue = r.PrevSignatureValue,
                QrCodePayload = r.QrCodePayload,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var paymentIds = receipts.Select(r => r.PaymentId).Distinct().ToList();

        // Pull only the payment fields we need (RKSV metadata + TSE signature).
        var paymentsQuery = _db.PaymentDetails.AsNoTracking()
            .Where(p => paymentIds.Contains(p.Id));
        var payments = await paymentsQuery
            .Select(p => new PaymentProjection
            {
                Id = p.Id,
                CashRegisterId = p.CashRegisterId,
                TseSignature = p.TseSignature,
                RksvSpecialReceiptKind = p.RksvSpecialReceiptKind,
                RksvSpecialReceiptYear = p.RksvSpecialReceiptYear,
                RksvSpecialReceiptMonth = p.RksvSpecialReceiptMonth,
                RksvNullbelegActsAsJahresbeleg = p.RksvNullbelegActsAsJahresbeleg,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var paymentsById = payments.ToDictionary(p => p.Id);

        // Resolve register numbers for human-readable display (single batched read).
        var registerIds = receipts.Select(r => r.CashRegisterId).Distinct().ToList();
        var registerNumbers = await _db.CashRegisters.AsNoTracking()
            .Where(c => registerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.RegisterNumber })
            .ToDictionaryAsync(c => c.Id, c => c.RegisterNumber, cancellationToken)
            .ConfigureAwait(false);

        var report = new RksvComplianceReportDto
        {
            GeneratedAtUtc = DateTime.UtcNow,
            CashRegisterId = cashRegisterId,
            FromUtc = fromUtc,
            ToUtc = toUtc,
        };

        report.SpecialReceipts.AddRange(BuildSpecialReceipts(receipts, paymentsById, registerNumbers));
        report.SignatureChain.AddRange(BuildSignatureChain(receipts, registerNumbers));
        report.SequenceGaps.AddRange(BuildSequenceGaps(receipts, registerNumbers));
        report.TseSignatureMissing.AddRange(BuildTseSignatureMissing(receipts, paymentsById, registerNumbers));
        report.QrPayloadValidation.AddRange(BuildQrValidation(receipts, registerNumbers));

        report.Summary = BuildSummary(receipts, report);

        _logger.LogInformation(
            "RKSV compliance report built: registers={Registers} receipts={Receipts} sonderbelege={Sonderbelege} chainBreaks={ChainBreaks} sequenceGaps={SequenceGaps} tseMissing={TseMissing} qrInvalid={QrInvalid}",
            report.Summary.RegistersCovered,
            report.Summary.FiscalReceiptsScanned,
            report.Summary.SpecialReceiptsCount,
            report.Summary.SignatureChainBreaks,
            report.Summary.SequenceGapCount,
            report.Summary.TseSignatureMissingCount,
            report.Summary.QrFormatInvalidCount);

        return report;
    }

    private static IEnumerable<RksvComplianceSpecialReceiptDto> BuildSpecialReceipts(
        IReadOnlyList<ReceiptProjection> receipts,
        IReadOnlyDictionary<Guid, PaymentProjection> paymentsById,
        IReadOnlyDictionary<Guid, string> registerNumbers)
    {
        foreach (var r in receipts)
        {
            if (!paymentsById.TryGetValue(r.PaymentId, out var p))
                continue;
            if (string.IsNullOrEmpty(p.RksvSpecialReceiptKind))
                continue;

            yield return new RksvComplianceSpecialReceiptDto
            {
                PaymentId = p.Id,
                ReceiptId = r.ReceiptId,
                ReceiptNumber = r.ReceiptNumber,
                Kind = p.RksvSpecialReceiptKind,
                NullbelegActsAsJahresbeleg = p.RksvNullbelegActsAsJahresbeleg,
                CashRegisterId = r.CashRegisterId,
                RegisterNumber = registerNumbers.TryGetValue(r.CashRegisterId, out var rn) ? rn : null,
                IssuedAtUtc = r.IssuedAt,
                Year = p.RksvSpecialReceiptYear,
                Month = p.RksvSpecialReceiptMonth,
                HasTseSignature = !string.IsNullOrWhiteSpace(p.TseSignature)
                                  && !string.IsNullOrWhiteSpace(r.SignatureValue),
            };
        }
    }

    private static IEnumerable<RksvComplianceSignatureChainItemDto> BuildSignatureChain(
        IReadOnlyList<ReceiptProjection> receipts,
        IReadOnlyDictionary<Guid, string> registerNumbers)
    {
        // Group by register; the input list is already ordered by (register, IssuedAt, ReceiptNumber).
        foreach (var registerGroup in receipts.GroupBy(r => r.CashRegisterId))
        {
            string? previousSignature = null;
            string? previousReceiptNumber = null;
            foreach (var r in registerGroup)
            {
                var status = RksvComplianceStatus.Pass;
                string? issue = null;

                if (previousSignature == null)
                {
                    // First receipt of the scan window for this register: cannot verify chain origin
                    // because the predecessor may be outside the window. Mark as Warn, not Fail.
                    if (!string.IsNullOrWhiteSpace(r.PrevSignatureValue))
                    {
                        // Acceptable: register has prior history; we just cannot confirm here.
                        status = RksvComplianceStatus.Warn;
                        issue = "First receipt in scan window: predecessor signature not in scope; chain origin not verified.";
                    }
                }
                else if (string.IsNullOrEmpty(r.PrevSignatureValue))
                {
                    status = RksvComplianceStatus.Fail;
                    issue = $"Previous signature value is empty; expected to chain to receipt {previousReceiptNumber}.";
                }
                else if (!string.Equals(r.PrevSignatureValue, previousSignature, StringComparison.Ordinal))
                {
                    status = RksvComplianceStatus.Fail;
                    issue = $"Previous signature value does not match previous receipt {previousReceiptNumber} signature.";
                }

                yield return new RksvComplianceSignatureChainItemDto
                {
                    CashRegisterId = r.CashRegisterId,
                    RegisterNumber = registerNumbers.TryGetValue(r.CashRegisterId, out var rn) ? rn : null,
                    ReceiptId = r.ReceiptId,
                    ReceiptNumber = r.ReceiptNumber,
                    IssuedAtUtc = r.IssuedAt,
                    SignaturePrefix = ToShortPrefix(r.SignatureValue),
                    PrevSignaturePrefix = ToShortPrefix(r.PrevSignatureValue),
                    ExpectedPrevSignaturePrefix = ToShortPrefix(previousSignature),
                    Status = status,
                    Issue = issue,
                };

                previousSignature = r.SignatureValue;
                previousReceiptNumber = r.ReceiptNumber;
            }
        }
    }

    private static IEnumerable<RksvComplianceSequenceGapDto> BuildSequenceGaps(
        IReadOnlyList<ReceiptProjection> receipts,
        IReadOnlyDictionary<Guid, string> registerNumbers)
    {
        // Parse (register, yyyyMMdd, seq) from receipt numbers. Only AT-…-…-{decimal seq} rows are inspected.
        var parsed = new List<(Guid CashRegisterId, DateTime Day, int Seq, string ReceiptNumber)>();
        foreach (var r in receipts)
        {
            if (!TryParseReceiptNumber(r.ReceiptNumber, out var day, out var seq))
                continue;
            parsed.Add((r.CashRegisterId, day, seq, r.ReceiptNumber));
        }

        var groups = parsed.GroupBy(x => (x.CashRegisterId, x.Day));
        foreach (var g in groups)
        {
            var ordered = g.OrderBy(x => x.Seq).ToList();
            // Detect gap from 1 -> first observed seq.
            if (ordered[0].Seq > 1)
            {
                for (var expected = 1; expected < ordered[0].Seq; expected++)
                {
                    yield return new RksvComplianceSequenceGapDto
                    {
                        CashRegisterId = g.Key.CashRegisterId,
                        RegisterNumber = registerNumbers.TryGetValue(g.Key.CashRegisterId, out var rn) ? rn : null,
                        SequenceDateUtc = g.Key.Day,
                        ExpectedSequence = expected,
                        PreviousReceiptNumber = null,
                        NextReceiptNumber = ordered[0].ReceiptNumber,
                    };
                }
            }

            for (var i = 1; i < ordered.Count; i++)
            {
                var prev = ordered[i - 1];
                var curr = ordered[i];
                for (var expected = prev.Seq + 1; expected < curr.Seq; expected++)
                {
                    yield return new RksvComplianceSequenceGapDto
                    {
                        CashRegisterId = g.Key.CashRegisterId,
                        RegisterNumber = registerNumbers.TryGetValue(g.Key.CashRegisterId, out var rn) ? rn : null,
                        SequenceDateUtc = g.Key.Day,
                        ExpectedSequence = expected,
                        PreviousReceiptNumber = prev.ReceiptNumber,
                        NextReceiptNumber = curr.ReceiptNumber,
                    };
                }
            }
        }
    }

    private static IEnumerable<RksvComplianceTseSignatureMissingDto> BuildTseSignatureMissing(
        IReadOnlyList<ReceiptProjection> receipts,
        IReadOnlyDictionary<Guid, PaymentProjection> paymentsById,
        IReadOnlyDictionary<Guid, string> registerNumbers)
    {
        foreach (var r in receipts)
        {
            paymentsById.TryGetValue(r.PaymentId, out var p);
            var paymentMissing = p == null || string.IsNullOrWhiteSpace(p.TseSignature);
            var receiptMissing = string.IsNullOrWhiteSpace(r.SignatureValue);

            if (!paymentMissing && !receiptMissing)
                continue;

            yield return new RksvComplianceTseSignatureMissingDto
            {
                PaymentId = r.PaymentId,
                ReceiptId = r.ReceiptId,
                ReceiptNumber = r.ReceiptNumber,
                CashRegisterId = r.CashRegisterId,
                RegisterNumber = registerNumbers.TryGetValue(r.CashRegisterId, out var rn) ? rn : null,
                IssuedAtUtc = r.IssuedAt,
                SpecialReceiptKind = p?.RksvSpecialReceiptKind,
                PaymentSignatureMissing = paymentMissing,
                ReceiptSignatureMissing = receiptMissing,
            };
        }
    }

    private IEnumerable<RksvComplianceQrValidationItemDto> BuildQrValidation(
        IReadOnlyList<ReceiptProjection> receipts,
        IReadOnlyDictionary<Guid, string> registerNumbers)
    {
        foreach (var r in receipts)
        {
            var payload = r.QrCodePayload;
            var item = new RksvComplianceQrValidationItemDto
            {
                ReceiptId = r.ReceiptId,
                ReceiptNumber = r.ReceiptNumber,
                CashRegisterId = r.CashRegisterId,
                RegisterNumber = registerNumbers.TryGetValue(r.CashRegisterId, out var rn) ? rn : null,
                IssuedAtUtc = r.IssuedAt,
            };

            if (string.IsNullOrWhiteSpace(payload))
            {
                item.QrPayloadMissing = true;
                item.IsValidFormat = false;
                item.Errors.Add("QR payload is missing on the receipt.");
                yield return item;
                continue;
            }

            var result = _qrValidator.Validate(payload);
            item.IsValidFormat = result.IsValidFormat;
            if (!result.IsValidFormat)
                item.Errors.AddRange(result.Errors);

            // Only emit rows that have something to flag (missing or invalid). Keeps the report compact.
            if (item.QrPayloadMissing || !item.IsValidFormat)
                yield return item;
        }
    }

    private static RksvComplianceReportSummaryDto BuildSummary(
        IReadOnlyList<ReceiptProjection> receipts,
        RksvComplianceReportDto report)
    {
        var summary = new RksvComplianceReportSummaryDto
        {
            RegistersCovered = receipts.Select(r => r.CashRegisterId).Distinct().Count(),
            FiscalReceiptsScanned = receipts.Count,
            SpecialReceiptsCount = report.SpecialReceipts.Count,
            SignatureChainBreaks = report.SignatureChain.Count(c => c.Status == RksvComplianceStatus.Fail),
            SequenceGapCount = report.SequenceGaps.Count,
            TseSignatureMissingCount = report.TseSignatureMissing.Count,
            QrFormatInvalidCount = report.QrPayloadValidation.Count(q => !q.QrPayloadMissing && !q.IsValidFormat),
            QrFormatMissingCount = report.QrPayloadValidation.Count(q => q.QrPayloadMissing),
        };

        summary.OverallPass =
            summary.SignatureChainBreaks == 0
            && summary.SequenceGapCount == 0
            && summary.TseSignatureMissingCount == 0
            && summary.QrFormatInvalidCount == 0;

        return summary;
    }

    private static bool TryParseReceiptNumber(string? receiptNumber, out DateTime day, out int seq)
    {
        day = default;
        seq = 0;
        if (string.IsNullOrWhiteSpace(receiptNumber))
            return false;

        var match = ReceiptNumberRegex.Match(receiptNumber);
        if (!match.Success)
            return false;

        if (!DateTime.TryParseExact(
                match.Groups["ymd"].Value,
                "yyyyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsedDate))
        {
            return false;
        }

        if (!int.TryParse(
                match.Groups["seq"].Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsedSeq))
        {
            return false;
        }

        day = DateTime.SpecifyKind(parsedDate.Date, DateTimeKind.Utc);
        seq = parsedSeq;
        return true;
    }

    private static string? ToShortPrefix(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;
        const int max = 24;
        return value.Length <= max ? value : value[..max] + "…";
    }

    /// <summary>Slim projection to avoid loading entire <see cref="Receipt"/> graph.</summary>
    private sealed class ReceiptProjection
    {
        public Guid ReceiptId { get; set; }
        public Guid PaymentId { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
        public Guid CashRegisterId { get; set; }
        public DateTime IssuedAt { get; set; }
        public string? SignatureValue { get; set; }
        public string? PrevSignatureValue { get; set; }
        public string? QrCodePayload { get; set; }
    }

    /// <summary>Slim projection over <see cref="PaymentDetails"/> for compliance fields only.</summary>
    private sealed class PaymentProjection
    {
        public Guid Id { get; set; }
        public Guid CashRegisterId { get; set; }
        public string TseSignature { get; set; } = string.Empty;
        public string? RksvSpecialReceiptKind { get; set; }
        public int? RksvSpecialReceiptYear { get; set; }
        public int? RksvSpecialReceiptMonth { get; set; }
        public bool RksvNullbelegActsAsJahresbeleg { get; set; }
    }
}
