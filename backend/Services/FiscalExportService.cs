using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Export;

namespace KasseAPI_Final.Services;

public class FiscalExportService : IFiscalExportService
{
    private const int MaxReceiptRows = 50_000;
    private static readonly TimeSpan MaxPeriod = TimeSpan.FromDays(366);

    private readonly AppDbContext _context;
    private readonly ILogger<FiscalExportService> _logger;

    public FiscalExportService(AppDbContext context, ILogger<FiscalExportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<FiscalExportPackageDto> BuildExportAsync(
        Guid cashRegisterId,
        DateTime fromUtc,
        DateTime toUtc,
        bool includeCsv,
        CancellationToken cancellationToken = default)
    {
        var from = NormalizeUtc(fromUtc);
        var to = NormalizeUtc(toUtc);
        if (to < from)
            throw new ArgumentException("toUtc must be >= fromUtc.", nameof(toUtc));
        if (to - from > MaxPeriod)
            throw new ArgumentException($"Period must not exceed {MaxPeriod.TotalDays} days.", nameof(toUtc));

        var register = await _context.CashRegisters
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cashRegisterId, cancellationToken);
        if (register == null)
            throw new InvalidOperationException($"Cash register {cashRegisterId} not found.");

        var chainState = await _context.SignatureChainState
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CashRegisterId == cashRegisterId, cancellationToken);

        var receipts = await _context.Receipts
            .AsNoTracking()
            .Where(r => r.CashRegisterId == cashRegisterId && r.IssuedAt >= from && r.IssuedAt <= to)
            .OrderBy(r => r.IssuedAt)
            .ThenBy(r => r.ReceiptNumber)
            .Take(MaxReceiptRows + 1)
            .Include(r => r.Payment)
            .ThenInclude(p => p.OfflineTransaction)
            .Include(r => r.Items)
            .Include(r => r.TaxLines)
            .ToListAsync(cancellationToken);

        if (receipts.Count > MaxReceiptRows)
        {
            _logger.LogWarning(
                "Fiscal export truncated: register {RegisterId}, period {From}–{To}, max {Max}",
                cashRegisterId, from, to, MaxReceiptRows);
            receipts = receipts.Take(MaxReceiptRows).ToList();
        }

        var closings = await _context.DailyClosings
            .AsNoTracking()
            .Where(c =>
                c.CashRegisterId == cashRegisterId &&
                c.CreatedAt >= from &&
                c.CreatedAt <= to)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        var receiptDtos = receipts.Select(MapReceipt).ToList();
        var chainLinks = receipts.Select(r => new FiscalReceiptChainLinkDto
        {
            ReceiptId = r.ReceiptId,
            ReceiptNumber = r.ReceiptNumber,
            IssuedAtUtc = r.IssuedAt,
            SignatureValue = r.SignatureValue,
            PrevSignatureValue = r.PrevSignatureValue
        }).ToList();

        var chainWarnings = FiscalExportChainContinuity.BuildWarnings(chainLinks).ToList();
        var signatureChainValid = chainWarnings.Count == 0;
        var sequenceContinuous = BuildReceiptSequenceContinuity(receipts.Select(r => r.ReceiptNumber).ToList());

        // Offline integrity metrics (diagnostics only).
        var offlineTransactionsInPeriod = await _context.OfflineTransactions
            .AsNoTracking()
            .Where(o => o.CashRegisterId == cashRegisterId && o.OfflineCreatedAtUtc >= from && o.OfflineCreatedAtUtc <= to)
            .ToListAsync(cancellationToken);

        var totalOfflineTransactions = offlineTransactionsInPeriod.Count;
        var syncedOfflineTransactions = offlineTransactionsInPeriod.Count(o => o.Status == OfflineTransactionStatus.Synced);
        var failedOfflineTransactions = offlineTransactionsInPeriod.Count(o => o.Status == OfflineTransactionStatus.Failed);
        var offlineReplayGaps = offlineTransactionsInPeriod.Count(o => o.Status == OfflineTransactionStatus.Pending);

        var closingDtos = closings.Select(MapClosing).ToList();

        var package = new FiscalExportPackageDto
        {
            GeneratedAtUtc = DateTime.UtcNow,
            CashRegisterId = cashRegisterId,
            RegisterNumber = register.RegisterNumber,
            RegisterLocation = register.Location,
            Period = new FiscalExportPeriodDto { FromUtc = from, ToUtc = to },
            SignatureChainState = chainState == null
                ? null
                : new FiscalSignatureChainStateDto
                {
                    CashRegisterId = chainState.CashRegisterId,
                    LastSignature = chainState.LastSignature,
                    LastCounter = chainState.LastCounter,
                    UpdatedAtUtc = chainState.UpdatedAt
                },
            ReceiptChain = chainLinks,
            Receipts = receiptDtos,
            Closings = closingDtos,
            ReceiptCount = receiptDtos.Count,
            ClosingCount = closingDtos.Count,
            ChainContinuityWarnings = chainWarnings,
            Integrity = new FiscalExportIntegrityDto
            {
                SignatureChainValid = signatureChainValid,
                SequenceContinuous = sequenceContinuous,
                OfflineReplayGaps = offlineReplayGaps,
                TotalOfflineTransactions = totalOfflineTransactions,
                SyncedOfflineTransactions = syncedOfflineTransactions,
                FailedOfflineTransactions = failedOfflineTransactions
            }
        };

        if (includeCsv)
        {
            package.ReceiptsCsv = BuildReceiptsCsv(receiptDtos);
            package.ClosingsCsv = BuildClosingsCsv(closingDtos);
        }

        _logger.LogInformation(
            "Fiscal export built: register {Register}, receipts {Rc}, closings {Cc}, csv {Csv}",
            register.RegisterNumber, package.ReceiptCount, package.ClosingCount, includeCsv);

        return package;
    }

    private static bool BuildReceiptSequenceContinuity(IReadOnlyList<string> receiptNumbersInOrder)
    {
        // Best-effort: validates that per-day receipt SEQ increments by 1 in the export order.
        string? currentDateSegment = null;
        int? prevSeq = null;

        foreach (var rn in receiptNumbersInOrder)
        {
            if (!TryParseReceiptNumber(rn, out var dateSegment, out var seq))
                continue;

            if (currentDateSegment == null)
            {
                currentDateSegment = dateSegment;
                prevSeq = seq;
                continue;
            }

            if (!string.Equals(currentDateSegment, dateSegment, StringComparison.Ordinal))
            {
                currentDateSegment = dateSegment;
                prevSeq = seq;
                continue;
            }

            if (prevSeq.HasValue && seq != prevSeq.Value + 1)
                return false;

            prevSeq = seq;
        }

        return true;
    }

    private static bool TryParseReceiptNumber(string receiptNumber, out string dateSegment, out int seq)
    {
        // Format: AT-{TSE_ID or register}-{YYYYMMDD}-{SEQ}
        dateSegment = string.Empty;
        seq = 0;
        if (string.IsNullOrWhiteSpace(receiptNumber))
            return false;

        var parts = receiptNumber.Split('-');
        if (parts.Length < 4)
            return false;

        dateSegment = parts[^2];
        var seqPart = parts[^1];
        if (!int.TryParse(seqPart, out seq))
            return false;

        return !string.IsNullOrWhiteSpace(dateSegment);
    }

    private static DateTime NormalizeUtc(DateTime dt)
    {
        return dt.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
            : dt.ToUniversalTime();
    }

    private static FiscalReceiptExportDto MapReceipt(Receipt r)
    {
        var p = r.Payment;
        return new FiscalReceiptExportDto
        {
            ReceiptId = r.ReceiptId,
            PaymentId = r.PaymentId,
            ReceiptNumber = r.ReceiptNumber,
            IssuedAtUtc = r.IssuedAt,
            CashierId = r.CashierId,
            CashRegisterId = r.CashRegisterId,
            SubTotal = r.SubTotal,
            TaxTotal = r.TaxTotal,
            GrandTotal = r.GrandTotal,
            QrCodePayload = r.QrCodePayload,
            SignatureValue = r.SignatureValue,
            PrevSignatureValue = r.PrevSignatureValue,
            SignatureFormat = r.SignatureFormat,
            JwsHeader = r.JwsHeader,
            JwsPayload = r.JwsPayload,
            JwsSignature = r.JwsSignature,
            Provider = r.Provider,
            CorrelationId = r.CorrelationId,
            CreatedAtUtc = r.CreatedAt,
            IsStorno = p?.IsStorno ?? false,
            IsRefund = p?.IsRefund ?? false,
            OriginalPaymentId = p?.OriginalPaymentId,
            OriginalReceiptId = p?.OriginalReceiptId,
            ReversalReason = p?.IsStorno == true ? p.CancellationReason : p?.IsRefund == true ? p.RefundReason : null,
            HasOfflineOrigin = p?.OfflineTransactionId != null,
            OfflineCreatedAtUtc = p?.OfflineTransaction?.OfflineCreatedAtUtc,
            FiscalizedAtUtc = p?.OfflineTransaction?.FiscalizedAtUtc,
            Items = r.Items.OrderBy(i => i.ItemId).Select(i => new FiscalReceiptItemExportDto
            {
                ItemId = i.ItemId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.TotalPrice,
                LineNet = i.LineNet,
                VatAmount = i.VatAmount,
                TaxRate = i.TaxRate,
                ParentItemId = i.ParentItemId,
                CategoryName = i.CategoryName
            }).ToList(),
            TaxLines = r.TaxLines.OrderBy(t => t.LineId).Select(t => new FiscalReceiptTaxLineExportDto
            {
                LineId = t.LineId,
                TaxType = t.TaxType,
                TaxRate = t.TaxRate,
                NetAmount = t.NetAmount,
                TaxAmount = t.TaxAmount,
                GrossAmount = t.GrossAmount
            }).ToList()
        };
    }

    private static FiscalClosingExportDto MapClosing(DailyClosing c)
    {
        return new FiscalClosingExportDto
        {
            Id = c.Id,
            CashRegisterId = c.CashRegisterId,
            UserId = c.UserId,
            ClosingDateUtc = c.ClosingDate,
            ClosingType = c.ClosingType,
            TotalAmount = c.TotalAmount,
            TotalTaxAmount = c.TotalTaxAmount,
            TransactionCount = c.TransactionCount,
            TseSignature = c.TseSignature,
            SignatureFormat = c.SignatureFormat,
            JwsHeader = c.JwsHeader,
            JwsPayload = c.JwsPayload,
            JwsSignature = c.JwsSignature,
            Provider = c.Provider,
            CorrelationId = c.CorrelationId,
            Status = c.Status,
            FinanzOnlineStatus = c.FinanzOnlineStatus,
            FinanzOnlineReferenceId = c.FinanzOnlineReferenceId,
            CreatedAtUtc = c.CreatedAt
        };
    }

    private static string BuildReceiptsCsv(IReadOnlyList<FiscalReceiptExportDto> receipts)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "receipt_id,payment_id,receipt_number,issued_at_utc,cashier_id,cash_register_id," +
            "sub_total,tax_total,grand_total,signature_value,prev_signature_value,signature_format," +
            "provider,correlation_id,created_at_utc,is_storno,is_refund,original_payment_id,original_receipt_id,reversal_reason," +
            "has_offline_origin,offline_created_at_utc,fiscalized_at_utc");
        var inv = CultureInfo.InvariantCulture;
        foreach (var r in receipts)
        {
            sb.Append(Csv(r.ReceiptId)).Append(',')
                .Append(Csv(r.PaymentId)).Append(',')
                .Append(Csv(r.ReceiptNumber)).Append(',')
                .Append(Csv(r.IssuedAtUtc.ToString("o", inv))).Append(',')
                .Append(Csv(r.CashierId)).Append(',')
                .Append(Csv(r.CashRegisterId)).Append(',')
                .Append(r.SubTotal.ToString(inv)).Append(',')
                .Append(r.TaxTotal.ToString(inv)).Append(',')
                .Append(r.GrandTotal.ToString(inv)).Append(',')
                .Append(Csv(r.SignatureValue)).Append(',')
                .Append(Csv(r.PrevSignatureValue)).Append(',')
                .Append(Csv(r.SignatureFormat)).Append(',')
                .Append(Csv(r.Provider)).Append(',')
                .Append(Csv(r.CorrelationId)).Append(',')
                .Append(Csv(r.CreatedAtUtc.ToString("o", inv))).Append(',')
                .Append(r.IsStorno ? "1" : "0").Append(',')
                .Append(r.IsRefund ? "1" : "0").Append(',')
                .Append(Csv(r.OriginalPaymentId?.ToString("D", inv))).Append(',')
                .Append(Csv(r.OriginalReceiptId?.ToString("D", inv))).Append(',')
                .Append(Csv(r.ReversalReason)).Append(',')
                .Append(r.HasOfflineOrigin ? "1" : "0").Append(',')
                .Append(Csv(r.OfflineCreatedAtUtc?.ToString("o", inv))).Append(',')
                .Append(Csv(r.FiscalizedAtUtc?.ToString("o", inv)))
                .AppendLine();
        }

        return sb.ToString();
    }

    private static string BuildClosingsCsv(IReadOnlyList<FiscalClosingExportDto> closings)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "closing_id,cash_register_id,user_id,closing_date_utc,closing_type,total_amount,total_tax_amount," +
            "transaction_count,tse_signature,status,finanz_online_status,finanz_online_reference_id,created_at_utc");
        var inv = CultureInfo.InvariantCulture;
        foreach (var c in closings)
        {
            sb.Append(Csv(c.Id)).Append(',')
                .Append(Csv(c.CashRegisterId)).Append(',')
                .Append(Csv(c.UserId)).Append(',')
                .Append(Csv(c.ClosingDateUtc.ToString("o", inv))).Append(',')
                .Append(Csv(c.ClosingType)).Append(',')
                .Append(c.TotalAmount.ToString(inv)).Append(',')
                .Append(c.TotalTaxAmount.ToString(inv)).Append(',')
                .Append(c.TransactionCount.ToString(inv)).Append(',')
                .Append(Csv(c.TseSignature)).Append(',')
                .Append(Csv(c.Status)).Append(',')
                .Append(Csv(c.FinanzOnlineStatus)).Append(',')
                .Append(Csv(c.FinanzOnlineReferenceId)).Append(',')
                .Append(Csv(c.CreatedAtUtc.ToString("o", inv)))
                .AppendLine();
        }

        return sb.ToString();
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        if (value.Contains('"', StringComparison.Ordinal) || value.Contains(',', StringComparison.Ordinal) ||
            value.Contains('\n', StringComparison.Ordinal) || value.Contains('\r', StringComparison.Ordinal))
            return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        return value;
    }

    private static string Csv(Guid g) => g.ToString("D", CultureInfo.InvariantCulture);
}
