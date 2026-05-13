using System.Globalization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KasseAPI_Final.Services;

/// <summary>
/// RKSV-safe receipt reprint: uses persisted receipt number, TSE payload/signature snapshot, and stored QR string only.
/// </summary>
public sealed class ReceiptPdfService : IReceiptPdfService
{
    /// <summary>Single-line watermark label for logs or future reuse.</summary>
    public const string ReprintWatermarkPrimary = "NACHAUSDRUCK";

    /// <summary>Thermal roll target width (~80 mm ≈ 288 pt at 72 dpi).</summary>
    private const float ThermalRollWidthPoints = 288f;

    private const float PageMarginPoints = 6f;
    private const int MaxItemRows = 8;
    private const int QrFallbackPayloadMaxChars = 100;
    private const int SignaturePreviewMaxChars = 40;
    private const int ProductNameMaxChars = 20;

    private readonly AppDbContext _context;
    private readonly IQrImageService _qrService;
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly ILogger<ReceiptPdfService> _logger;

    static ReceiptPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public ReceiptPdfService(
        AppDbContext context,
        IQrImageService qrService,
        ISettingsTenantResolver tenantResolver,
        ILogger<ReceiptPdfService> logger)
    {
        _context = context;
        _qrService = qrService;
        _tenantResolver = tenantResolver;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<byte[]> GenerateReprintPdfAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);

        var receipt = await _context.Receipts.AsNoTracking()
            .Include(r => r.Items)
            .Include(r => r.Payment!)
                .ThenInclude(p => p!.CashRegister)
            .Where(r => r.PaymentId == paymentId)
            .Where(r => _context.CashRegisters.Any(cr => cr.Id == r.CashRegisterId && cr.TenantId == tenantId))
            .FirstOrDefaultAsync(cancellationToken);

        if (receipt?.Payment == null)
            throw new KeyNotFoundException($"Receipt for payment {paymentId} was not found.");

        var payment = receipt.Payment;
        var registerLabel = payment.CashRegister?.RegisterNumber ?? payment.CashRegisterId.ToString();
        var qrPayload = receipt.QrCodePayload?.Trim();

        byte[]? qrEmbedPng = null;
        string? qrFallbackChunk = null;
        if (!string.IsNullOrEmpty(qrPayload))
        {
            try
            {
                var rawPng = _qrService.GetQrPngFromExactPayload(qrPayload);
                qrEmbedPng = SelectQrPngForEmbedding(rawPng);
                if (qrEmbedPng == null && rawPng is { Length: > 0 })
                {
                    var relaxed = SelectQrPngForEmbedding(_qrService.GenerateQrCodeImage(qrPayload));
                    if (relaxed != null)
                        qrEmbedPng = relaxed;
                }

                if (qrEmbedPng == null)
                {
                    if (rawPng is { Length: > 0 })
                    {
                        _logger.LogWarning(
                            "QR bytes are not a valid PNG for PDF embedding (payment {PaymentId}, {ByteLen} bytes)",
                            paymentId,
                            rawPng.Length);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "QR PNG encoding returned empty for reprint (payment {PaymentId}, payload length {Len})",
                            paymentId,
                            qrPayload.Length);
                    }

                    qrFallbackChunk = BuildQrFallbackText(qrPayload);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "QR generation failed for reprint PDF (payment {PaymentId}); using text fallback", paymentId);
                qrFallbackChunk = BuildQrFallbackText(qrPayload);
            }
        }

        var deAt = CultureInfo.GetCultureInfo("de-AT");
        var inv = CultureInfo.InvariantCulture;
        var belegZeit = FormatAustriaLocal(payment.TseTimestamp);
        var paymentMethodLabel = FormatPaymentMethod(payment.PaymentMethodRaw);
        var specialKind = payment.RksvSpecialReceiptKind?.Trim();

        var orderedItems = receipt.Items
            .OrderBy(i => i.ParentItemId == null ? 0 : 1)
            .ThenBy(i => i.ProductName)
            .ThenBy(i => i.ItemId)
            .ToList();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.ContinuousSize(ThermalRollWidthPoints);
                page.MarginHorizontal(PageMarginPoints);
                page.MarginVertical(PageMarginPoints);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Courier New"));

                page.Content().Column(column =>
                {
                    column.Spacing(2);

                    column.Item().AlignCenter().Text(ReprintWatermarkPrimary)
                        .FontSize(8).Italic().FontColor(Colors.Grey.Darken2);
                    column.Item().AlignCenter().Text("(kein Original)")
                        .FontSize(7).Italic().FontColor(Colors.Grey.Darken2);
                    column.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

                    column.Spacing(3);

                    column.Item().Text(registerLabel);
                    column.Item().Text($"Nr: {receipt.ReceiptNumber}");
                    column.Item().Text(belegZeit);
                    if (!string.IsNullOrEmpty(specialKind))
                        column.Item().Text(GetSpecialReceiptDisplayName(specialKind));

                    column.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

                    if (orderedItems.Count > 0)
                    {
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.ConstantColumn(36);
                                columns.ConstantColumn(44);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(CellHeader).Text("Artikel");
                                header.Cell().Element(CellHeader).AlignRight().Text("Menge");
                                header.Cell().Element(CellHeader).AlignRight().Text("€");
                            });

                            foreach (var item in orderedItems.Take(MaxItemRows))
                            {
                                var name = Ellipsize(item.ProductName, ProductNameMaxChars);
                                table.Cell().Text(name);
                                table.Cell().AlignRight().Text(item.Quantity.ToString(inv));
                                table.Cell().AlignRight().Text(item.TotalPrice.ToString("F2", deAt));
                            }

                            if (orderedItems.Count > MaxItemRows)
                            {
                                table.Cell().ColumnSpan(3).Text($"(+ {orderedItems.Count - MaxItemRows} weitere Artikel)")
                                    .FontSize(7).Italic();
                            }
                        });

                        column.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
                    }

                    column.Item().AlignRight().Text($"Gesamt: {payment.TotalAmount.ToString("F2", deAt)} €").SemiBold();
                    column.Item().AlignRight().Text($"Zahlung: {paymentMethodLabel}");

                    column.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

                    if (qrEmbedPng != null)
                    {
                        column.Item().AlignCenter()
                            .Width(200)
                            .Image(qrEmbedPng)
                            .FitArea();
                    }
                    else if (!string.IsNullOrEmpty(qrFallbackChunk))
                    {
                        column.Item().AlignCenter().Text("[QR-Daten]").FontSize(7).FontColor(Colors.Grey.Darken2);
                        foreach (var line in ChunkForThermal(qrFallbackChunk, 26))
                            column.Item().AlignCenter().Text(line).FontSize(5).FontColor(Colors.Grey.Darken1);
                    }

                    var sigPreview = TruncateSignature(payment.TseSignature, SignaturePreviewMaxChars);
                    column.Item().Text($"Sig: {sigPreview}").FontSize(6).FontColor(Colors.Grey.Darken2);
                });
            });
        }).GeneratePdf();
    }

    private static byte[]? SelectQrPngForEmbedding(byte[]? png) =>
        png is { Length: >= 8 } && HasPngSignature(png) ? png : null;

    private static bool HasPngSignature(ReadOnlySpan<byte> data) =>
        data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47
        && data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A;

    private static string BuildQrFallbackText(string payload)
    {
        if (payload.Length <= QrFallbackPayloadMaxChars)
            return payload;
        return string.Concat(payload.AsSpan(0, QrFallbackPayloadMaxChars), "...");
    }

    private static IEnumerable<string> ChunkForThermal(string text, int chunkLen)
    {
        if (string.IsNullOrEmpty(text) || chunkLen <= 0)
            yield break;

        for (var i = 0; i < text.Length; i += chunkLen)
        {
            var len = Math.Min(chunkLen, text.Length - i);
            yield return text.Substring(i, len);
        }
    }

    private static string Ellipsize(string value, int maxChars)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        if (value.Length <= maxChars)
            return value;
        if (maxChars <= 3)
            return value[..maxChars];
        return string.Concat(value.AsSpan(0, maxChars - 3), "...");
    }

    private static string GetSpecialReceiptDisplayName(string kind) =>
        kind switch
        {
            RksvSpecialReceiptKinds.Nullbeleg => "Nullbeleg",
            RksvSpecialReceiptKinds.Startbeleg => "Startbeleg",
            RksvSpecialReceiptKinds.Monatsbeleg => "Monatsbeleg",
            RksvSpecialReceiptKinds.Jahresbeleg => "Jahresbeleg",
            RksvSpecialReceiptKinds.Schlussbeleg => "Endbeleg",
            _ => kind
        };

    private static IContainer CellHeader(IContainer c) =>
        c.DefaultTextStyle(x => x.SemiBold().FontSize(8)).PaddingVertical(1);

    private static string FormatAustriaLocal(DateTime value)
    {
        try
        {
            var tzId = OperatingSystem.IsWindows() ? "W. Europe Standard Time" : "Europe/Vienna";
            var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
            var utc = value.Kind == DateTimeKind.Utc
                ? value
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
            var local = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
            return local.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.GetCultureInfo("de-AT"));
        }
        catch
        {
            return value.ToUniversalTime().ToString("dd.MM.yyyy HH:mm:ss' UTC'", CultureInfo.InvariantCulture);
        }
    }

    private static string FormatPaymentMethod(string raw)
    {
        if (int.TryParse(raw, out var methodInt) && Enum.IsDefined(typeof(PaymentMethod), methodInt))
            return ((PaymentMethod)methodInt).ToString();
        return string.IsNullOrWhiteSpace(raw) ? "—" : raw;
    }

    private static string TruncateSignature(string? signature, int maxChars)
    {
        if (string.IsNullOrEmpty(signature))
            return "keine Signatur";
        if (signature.Length <= maxChars)
            return signature;
        if (maxChars <= 3)
            return signature[..maxChars];
        return string.Concat(signature.AsSpan(0, maxChars - 3), "...");
    }
}
