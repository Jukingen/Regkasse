using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <summary>
/// Composes the internal RKSV evidence bundle:
/// NOTICE.txt + manifest.json + compliance-report.json + payment-details.csv +
/// receipts.json + signature-chain-state.json + tse-signatures.json (optional).
/// Bundle is for internal audit / BMF presentation; not the official DEP export.
/// </summary>
public sealed class RksvEvidenceBundleService : IRksvEvidenceBundleService
{
    /// <summary>Auditor-visible note included verbatim in <c>NOTICE.txt</c> and the manifest.</summary>
    public const string AuditorNoticeEn =
        "This is for internal compliance evidence. Official DEP export is separate.";

    /// <summary>Mandatory diagnostic disclaimer in German (mirrors compliance-report and fiscal-export wording).</summary>
    public const string LegalNoticeDe =
        "RECHTLICHER HINWEIS: Dieses Bündel ist kein rechtsverbindlicher RKSV-/Finanzamt-Beleg "
        + "nach § 8 RKSV. Nur für interne Compliance-Zwecke. Originalbeleg mit TSE-Signatur ist maßgeblich. "
        + "Der amtliche DEP-Export ist ein separates Verfahren.";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly AppDbContext _db;
    private readonly IRksvComplianceReportService _complianceReportService;
    private readonly ILogger<RksvEvidenceBundleService> _logger;

    public RksvEvidenceBundleService(
        AppDbContext db,
        IRksvComplianceReportService complianceReportService,
        ILogger<RksvEvidenceBundleService> logger)
    {
        _db = db;
        _complianceReportService = complianceReportService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<RksvEvidenceBundleResultDto> BuildBundleAsync(
        RksvEvidenceBundleRequestDto request,
        string generatedByUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.FromUtc >= request.ToUtc)
            throw new ArgumentException("fromUtc must be strictly less than toUtc.", nameof(request));

        var inv = CultureInfo.InvariantCulture;
        var registerFilter = request.CashRegisterId == Guid.Empty ? null : request.CashRegisterId;

        // Compliance report (PROMPT 1) is reused verbatim — single source of truth for findings.
        var complianceReport = await _complianceReportService
            .BuildReportAsync(registerFilter, request.FromUtc, request.ToUtc, cancellationToken)
            .ConfigureAwait(false);

        // Read-only projections for the bundle datasets.
        var payments = await LoadPaymentsAsync(registerFilter, request.FromUtc, request.ToUtc, cancellationToken)
            .ConfigureAwait(false);
        var receipts = await LoadReceiptsAsync(registerFilter, request.FromUtc, request.ToUtc,
                request.IncludeReceiptItems, cancellationToken)
            .ConfigureAwait(false);
        var signatureChainState = await LoadSignatureChainStateAsync(receipts, cancellationToken)
            .ConfigureAwait(false);
        var tseSignatures = request.IncludeTseSignatureLog
            ? await LoadTseSignaturesAsync(registerFilter, request.FromUtc, request.ToUtc, cancellationToken)
                .ConfigureAwait(false)
            : new List<TseSignatureRow>();

        var generatedAtUtc = DateTime.UtcNow;
        var manifest = new RksvEvidenceBundleManifestDto
        {
            BundleVersion = "1.0",
            GeneratedAtUtc = generatedAtUtc,
            CashRegisterId = registerFilter,
            FromUtc = request.FromUtc,
            ToUtc = request.ToUtc,
            GeneratedByUserId = string.IsNullOrWhiteSpace(generatedByUserId) ? "unknown" : generatedByUserId,
            LegalNoticeDe = LegalNoticeDe,
            AuditorNoticeEn = AuditorNoticeEn,
            Counts = new RksvEvidenceBundleCountsDto
            {
                PaymentDetailRows = payments.Count,
                ReceiptRows = receipts.Count,
                SignatureChainStateRows = signatureChainState.Count,
                TseSignatureRows = tseSignatures.Count,
            },
        };

        // Build zip in memory; ZipArchive needs the stream to remain open during writes.
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var noticeBytes = Encoding.UTF8.GetBytes(BuildNoticeText(manifest, inv));
            WriteEntry(archive, "NOTICE.txt", noticeBytes);
            manifest.Files.Add(new RksvEvidenceBundleFileEntryDto
            {
                Name = "NOTICE.txt",
                ContentType = "text/plain; charset=utf-8",
                Description = "German + English disclaimer; auditor note.",
                Rows = 0,
                SizeBytes = noticeBytes.Length,
            });

            var complianceBytes = JsonSerializer.SerializeToUtf8Bytes(complianceReport, JsonOpts);
            WriteEntry(archive, "compliance-report.json", complianceBytes);
            manifest.Files.Add(new RksvEvidenceBundleFileEntryDto
            {
                Name = "compliance-report.json",
                ContentType = "application/json",
                Description = "Diagnostic RKSV compliance findings (5 checks).",
                Rows = complianceReport.SpecialReceipts.Count
                       + complianceReport.SignatureChain.Count
                       + complianceReport.SequenceGaps.Count
                       + complianceReport.TseSignatureMissing.Count
                       + complianceReport.QrPayloadValidation.Count,
                SizeBytes = complianceBytes.Length,
            });

            var paymentsCsv = BuildPaymentDetailsCsv(payments, inv);
            WriteEntry(archive, "payment-details.csv", paymentsCsv);
            manifest.Files.Add(new RksvEvidenceBundleFileEntryDto
            {
                Name = "payment-details.csv",
                ContentType = "text/csv; charset=utf-8",
                Description = "Flat payment_details (no raw JSONB; voucher codes never stored).",
                Rows = payments.Count,
                SizeBytes = paymentsCsv.Length,
            });

            var receiptsBytes = JsonSerializer.SerializeToUtf8Bytes(receipts, JsonOpts);
            WriteEntry(archive, "receipts.json", receiptsBytes);
            manifest.Files.Add(new RksvEvidenceBundleFileEntryDto
            {
                Name = "receipts.json",
                ContentType = "application/json",
                Description = "Receipts including items and tax lines (UTC issue time range).",
                Rows = receipts.Count,
                SizeBytes = receiptsBytes.Length,
            });

            var chainBytes = JsonSerializer.SerializeToUtf8Bytes(signatureChainState, JsonOpts);
            WriteEntry(archive, "signature-chain-state.json", chainBytes);
            manifest.Files.Add(new RksvEvidenceBundleFileEntryDto
            {
                Name = "signature-chain-state.json",
                ContentType = "application/json",
                Description = "Per-register TSE signature chain state at bundle generation time.",
                Rows = signatureChainState.Count,
                SizeBytes = chainBytes.Length,
            });

            if (request.IncludeTseSignatureLog)
            {
                var tseBytes = JsonSerializer.SerializeToUtf8Bytes(tseSignatures, JsonOpts);
                WriteEntry(archive, "tse-signatures.json", tseBytes);
                manifest.Files.Add(new RksvEvidenceBundleFileEntryDto
                {
                    Name = "tse-signatures.json",
                    ContentType = "application/json",
                    Description = "TSE signature log entries within the requested UTC range.",
                    Rows = tseSignatures.Count,
                    SizeBytes = tseBytes.Length,
                });
            }

            // Manifest must be written last so it captures all sibling file sizes.
            var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOpts);
            WriteEntry(archive, "manifest.json", manifestBytes);
        }

        ms.Position = 0;
        var zipBytes = ms.ToArray();
        var fileName = BuildBundleFileName(registerFilter, request.FromUtc, request.ToUtc, generatedAtUtc, inv);

        _logger.LogInformation(
            "RKSV evidence bundle generated: register={Register} from={FromUtc} to={ToUtc} payments={Payments} receipts={Receipts} chainStateRows={Chain} tseRows={Tse} sizeBytes={Size}",
            registerFilter,
            request.FromUtc,
            request.ToUtc,
            payments.Count,
            receipts.Count,
            signatureChainState.Count,
            tseSignatures.Count,
            zipBytes.Length);

        return new RksvEvidenceBundleResultDto
        {
            ZipBytes = zipBytes,
            FileName = fileName,
            Manifest = manifest,
        };
    }

    private async Task<List<PaymentRow>> LoadPaymentsAsync(
        Guid? registerFilter,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        // Use the receipt issue time range to bound payments via Receipt.PaymentId.
        // Payments without a Receipt (rare but possible for legacy rows) are still fetched
        // when their TseTimestamp falls in range, so the bundle does not silently drop them.
        var paymentIdsByReceipt = _db.Receipts.AsNoTracking()
            .Where(r => r.IssuedAt >= fromUtc && r.IssuedAt < toUtc);
        if (registerFilter.HasValue)
            paymentIdsByReceipt = paymentIdsByReceipt.Where(r => r.CashRegisterId == registerFilter.Value);

        var paymentIds = await paymentIdsByReceipt
            .Select(r => r.PaymentId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var query = _db.PaymentDetails.AsNoTracking()
            .Where(p => paymentIds.Contains(p.Id)
                        || (p.TseTimestamp >= fromUtc && p.TseTimestamp < toUtc));
        if (registerFilter.HasValue)
            query = query.Where(p => p.CashRegisterId == registerFilter.Value);

        return await query
            .OrderBy(p => p.TseTimestamp)
            .ThenBy(p => p.ReceiptNumber)
            .Select(p => new PaymentRow
            {
                Id = p.Id,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                TseTimestamp = p.TseTimestamp,
                CustomerId = p.CustomerId,
                CustomerName = p.CustomerName,
                CashierId = p.CashierId,
                CashRegisterId = p.CashRegisterId,
                ReceiptNumber = p.ReceiptNumber,
                TotalAmount = p.TotalAmount,
                TaxAmount = p.TaxAmount,
                PaymentMethodRaw = p.PaymentMethodRaw,
                Steuernummer = p.Steuernummer,
                TseSignature = p.TseSignature,
                PrevSignatureValueUsed = p.PrevSignatureValueUsed,
                IsRefund = p.IsRefund,
                IsStorno = p.IsStorno,
                StornoReason = p.StornoReason.HasValue ? p.StornoReason.Value.ToString() : null,
                OriginalPaymentId = p.OriginalPaymentId,
                OriginalReceiptId = p.OriginalReceiptId,
                RksvSpecialReceiptKind = p.RksvSpecialReceiptKind,
                RksvSpecialReceiptYear = p.RksvSpecialReceiptYear,
                RksvSpecialReceiptMonth = p.RksvSpecialReceiptMonth,
                RksvNullbelegActsAsJahresbeleg = p.RksvNullbelegActsAsJahresbeleg,
                FinanzOnlineStatus = p.FinanzOnlineStatus,
                FinanzOnlineReferenceId = p.FinanzOnlineReferenceId,
                IsPrinted = p.IsPrinted,
                Provider = p.Provider,
                CorrelationId = p.CorrelationId,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<List<ReceiptRow>> LoadReceiptsAsync(
        Guid? registerFilter,
        DateTime fromUtc,
        DateTime toUtc,
        bool includeItems,
        CancellationToken cancellationToken)
    {
        var query = _db.Receipts.AsNoTracking()
            .Where(r => r.IssuedAt >= fromUtc && r.IssuedAt < toUtc);
        if (registerFilter.HasValue)
            query = query.Where(r => r.CashRegisterId == registerFilter.Value);

        if (includeItems)
        {
            return await query
                .Include(r => r.Items)
                .Include(r => r.TaxLines)
                .OrderBy(r => r.CashRegisterId)
                .ThenBy(r => r.IssuedAt)
                .ThenBy(r => r.ReceiptNumber)
                .Select(r => new ReceiptRow
                {
                    ReceiptId = r.ReceiptId,
                    PaymentId = r.PaymentId,
                    ReceiptNumber = r.ReceiptNumber,
                    IssuedAt = r.IssuedAt,
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
                    CreatedAt = r.CreatedAt,
                    Items = r.Items.Select(i => new ReceiptItemRow
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
                        CategoryName = i.CategoryName,
                    }).ToList(),
                    TaxLines = r.TaxLines.Select(t => new ReceiptTaxLineRow
                    {
                        LineId = t.LineId,
                        TaxType = t.TaxType,
                        TaxRate = t.TaxRate,
                        NetAmount = t.NetAmount,
                        TaxAmount = t.TaxAmount,
                        GrossAmount = t.GrossAmount,
                    }).ToList(),
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return await query
            .OrderBy(r => r.CashRegisterId)
            .ThenBy(r => r.IssuedAt)
            .ThenBy(r => r.ReceiptNumber)
            .Select(r => new ReceiptRow
            {
                ReceiptId = r.ReceiptId,
                PaymentId = r.PaymentId,
                ReceiptNumber = r.ReceiptNumber,
                IssuedAt = r.IssuedAt,
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
                CreatedAt = r.CreatedAt,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<List<SignatureChainStateRow>> LoadSignatureChainStateAsync(
        IReadOnlyList<ReceiptRow> receipts,
        CancellationToken cancellationToken)
    {
        var registerIds = receipts.Select(r => r.CashRegisterId).Distinct().ToList();
        if (registerIds.Count == 0)
            return new List<SignatureChainStateRow>();

        return await _db.SignatureChainState.AsNoTracking()
            .Where(s => registerIds.Contains(s.CashRegisterId))
            .OrderBy(s => s.CashRegisterId)
            .Select(s => new SignatureChainStateRow
            {
                Id = s.Id,
                CashRegisterId = s.CashRegisterId,
                LastSignature = s.LastSignature,
                LastCounter = s.LastCounter,
                UpdatedAt = s.UpdatedAt,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<List<TseSignatureRow>> LoadTseSignaturesAsync(
        Guid? registerFilter,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var query = _db.TseSignatures.AsNoTracking()
            .Where(t => t.CreatedAt >= fromUtc && t.CreatedAt < toUtc);
        if (registerFilter.HasValue)
            query = query.Where(t => t.CashRegisterId == registerFilter.Value);

        return await query
            .OrderBy(t => t.CreatedAt)
            .Select(t => new TseSignatureRow
            {
                Id = t.Id,
                CashRegisterId = t.CashRegisterId,
                InvoiceNumber = t.InvoiceNumber,
                Amount = t.Amount,
                SignatureType = t.SignatureType,
                Signature = t.Signature,
                TseDeviceId = t.TseDeviceId,
                CertificateNumber = t.CertificateNumber,
                CreatedAt = t.CreatedAt,
                ValidatedAt = t.ValidatedAt,
                IsValid = t.IsValid,
                ValidationError = t.ValidationError,
                SignatureFormat = t.SignatureFormat,
                JwsHeader = t.JwsHeader,
                JwsPayload = t.JwsPayload,
                JwsSignature = t.JwsSignature,
                Provider = t.Provider,
                CorrelationId = t.CorrelationId,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static byte[] BuildPaymentDetailsCsv(IReadOnlyList<PaymentRow> rows, CultureInfo inv)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", new[]
        {
            "Id", "CreatedAt", "UpdatedAt", "TseTimestamp",
            "CustomerId", "CustomerName", "CashierId", "CashRegisterId",
            "ReceiptNumber", "TotalAmount", "TaxAmount", "PaymentMethodRaw", "Steuernummer",
            "TseSignature", "PrevSignatureValueUsed",
            "IsRefund", "IsStorno", "StornoReason", "OriginalPaymentId", "OriginalReceiptId",
            "RksvSpecialReceiptKind", "RksvSpecialReceiptYear", "RksvSpecialReceiptMonth", "RksvNullbelegActsAsJahresbeleg",
            "FinanzOnlineStatus", "FinanzOnlineReferenceId",
            "IsPrinted", "Provider", "CorrelationId",
        }));

        foreach (var r in rows)
        {
            sb.Append(string.Join(",", new[]
            {
                Csv(r.Id.ToString("D", inv)),
                Csv(r.CreatedAt.ToUniversalTime().ToString("o", inv)),
                Csv(r.UpdatedAt?.ToUniversalTime().ToString("o", inv)),
                Csv(r.TseTimestamp.ToUniversalTime().ToString("o", inv)),
                Csv(r.CustomerId.ToString("D", inv)),
                Csv(r.CustomerName),
                Csv(r.CashierId),
                Csv(r.CashRegisterId.ToString("D", inv)),
                Csv(r.ReceiptNumber),
                Csv(r.TotalAmount.ToString("F2", inv)),
                Csv(r.TaxAmount.ToString("F2", inv)),
                Csv(r.PaymentMethodRaw),
                Csv(r.Steuernummer),
                Csv(r.TseSignature),
                Csv(r.PrevSignatureValueUsed),
                Csv(r.IsRefund ? "true" : "false"),
                Csv(r.IsStorno ? "true" : "false"),
                Csv(r.StornoReason),
                Csv(r.OriginalPaymentId?.ToString("D", inv)),
                Csv(r.OriginalReceiptId?.ToString("D", inv)),
                Csv(r.RksvSpecialReceiptKind),
                Csv(r.RksvSpecialReceiptYear?.ToString(inv)),
                Csv(r.RksvSpecialReceiptMonth?.ToString(inv)),
                Csv(r.RksvNullbelegActsAsJahresbeleg ? "true" : "false"),
                Csv(r.FinanzOnlineStatus),
                Csv(r.FinanzOnlineReferenceId),
                Csv(r.IsPrinted ? "true" : "false"),
                Csv(r.Provider),
                Csv(r.CorrelationId),
            }));
            sb.AppendLine();
        }

        // UTF-8 BOM for Excel compatibility (mirrors AdminFiscalExportAuditController CSV exporter).
        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var bytes = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);
        return bytes;
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        var v = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{v}\"";
    }

    private static string BuildNoticeText(RksvEvidenceBundleManifestDto manifest, CultureInfo inv)
    {
        var sb = new StringBuilder();
        sb.AppendLine("RKSV Evidence Bundle (internal compliance evidence)");
        sb.AppendLine("====================================================");
        sb.AppendLine();
        sb.AppendLine("AUDITOR NOTE (English):");
        sb.AppendLine(AuditorNoticeEn);
        sb.AppendLine();
        sb.AppendLine("RECHTLICHER HINWEIS (Deutsch):");
        sb.AppendLine(LegalNoticeDe);
        sb.AppendLine();
        sb.AppendLine("Bundle metadata:");
        sb.AppendLine($"  Generated (UTC): {manifest.GeneratedAtUtc.ToString("o", inv)}");
        sb.AppendLine($"  Generated by:    {manifest.GeneratedByUserId}");
        sb.AppendLine($"  Cash register:   {(manifest.CashRegisterId.HasValue ? manifest.CashRegisterId.Value.ToString("D", inv) : "(all registers)")}");
        sb.AppendLine($"  From (UTC):      {manifest.FromUtc.ToString("o", inv)}");
        sb.AppendLine($"  To (UTC):        {manifest.ToUtc.ToString("o", inv)}");
        sb.AppendLine($"  Bundle version:  {manifest.BundleVersion}");
        sb.AppendLine();
        sb.AppendLine("Files in this bundle:");
        sb.AppendLine("  manifest.json               Machine-readable bundle index.");
        sb.AppendLine("  compliance-report.json      Diagnostic RKSV findings (5 checks).");
        sb.AppendLine("  payment-details.csv         Flat payment_details for the requested UTC range.");
        sb.AppendLine("  receipts.json               Receipts including items and tax lines.");
        sb.AppendLine("  signature-chain-state.json  Per-register TSE signature chain state.");
        sb.AppendLine("  tse-signatures.json         TSE signature log entries (when included).");
        return sb.ToString();
    }

    private static string BuildBundleFileName(
        Guid? cashRegisterId,
        DateTime fromUtc,
        DateTime toUtc,
        DateTime generatedAtUtc,
        CultureInfo inv)
    {
        var registerSegment = cashRegisterId.HasValue
            ? cashRegisterId.Value.ToString("D", inv)
            : "all-registers";
        var fromSeg = fromUtc.ToString("yyyyMMdd", inv);
        var toSeg = toUtc.ToString("yyyyMMdd", inv);
        var stamp = generatedAtUtc.ToString("yyyyMMdd_HHmmss", inv);
        return $"rksv-evidence-bundle_{registerSegment}_{fromSeg}-{toSeg}_{stamp}_UTC.zip";
    }

    private static void WriteEntry(ZipArchive archive, string name, byte[] bytes)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var es = entry.Open();
        es.Write(bytes, 0, bytes.Length);
    }

    /// <summary>Slim flat row for <c>payment-details.csv</c>; excludes JSONB blobs by design.</summary>
    private sealed class PaymentRow
    {
        public Guid Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime TseTimestamp { get; set; }
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CashierId { get; set; }
        public Guid CashRegisterId { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public string PaymentMethodRaw { get; set; } = string.Empty;
        public string Steuernummer { get; set; } = string.Empty;
        public string TseSignature { get; set; } = string.Empty;
        public string? PrevSignatureValueUsed { get; set; }
        public bool IsRefund { get; set; }
        public bool IsStorno { get; set; }
        public string? StornoReason { get; set; }
        public Guid? OriginalPaymentId { get; set; }
        public Guid? OriginalReceiptId { get; set; }
        public string? RksvSpecialReceiptKind { get; set; }
        public int? RksvSpecialReceiptYear { get; set; }
        public int? RksvSpecialReceiptMonth { get; set; }
        public bool RksvNullbelegActsAsJahresbeleg { get; set; }
        public string? FinanzOnlineStatus { get; set; }
        public string? FinanzOnlineReferenceId { get; set; }
        public bool IsPrinted { get; set; }
        public string? Provider { get; set; }
        public string? CorrelationId { get; set; }
    }

    /// <summary>Receipt projection for <c>receipts.json</c> (avoids loading the entire EF graph).</summary>
    private sealed class ReceiptRow
    {
        public Guid ReceiptId { get; set; }
        public Guid PaymentId { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; }
        public string? CashierId { get; set; }
        public Guid CashRegisterId { get; set; }
        public decimal SubTotal { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal GrandTotal { get; set; }
        public string? QrCodePayload { get; set; }
        public string? SignatureValue { get; set; }
        public string? PrevSignatureValue { get; set; }
        public string? SignatureFormat { get; set; }
        public string? JwsHeader { get; set; }
        public string? JwsPayload { get; set; }
        public string? JwsSignature { get; set; }
        public string? Provider { get; set; }
        public string? CorrelationId { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<ReceiptItemRow>? Items { get; set; }
        public List<ReceiptTaxLineRow>? TaxLines { get; set; }
    }

    private sealed class ReceiptItemRow
    {
        public Guid ItemId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal LineNet { get; set; }
        public decimal VatAmount { get; set; }
        public decimal TaxRate { get; set; }
        public Guid? ParentItemId { get; set; }
        public string? CategoryName { get; set; }
    }

    private sealed class ReceiptTaxLineRow
    {
        public Guid LineId { get; set; }
        public int TaxType { get; set; }
        public decimal TaxRate { get; set; }
        public decimal NetAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal GrossAmount { get; set; }
    }

    private sealed class SignatureChainStateRow
    {
        public Guid Id { get; set; }
        public Guid CashRegisterId { get; set; }
        public string? LastSignature { get; set; }
        public int LastCounter { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    private sealed class TseSignatureRow
    {
        public Guid Id { get; set; }
        public Guid CashRegisterId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string SignatureType { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
        public Guid? TseDeviceId { get; set; }
        public string? CertificateNumber { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ValidatedAt { get; set; }
        public bool IsValid { get; set; }
        public string? ValidationError { get; set; }
        public string? SignatureFormat { get; set; }
        public string? JwsHeader { get; set; }
        public string? JwsPayload { get; set; }
        public string? JwsSignature { get; set; }
        public string? Provider { get; set; }
        public string? CorrelationId { get; set; }
    }
}
