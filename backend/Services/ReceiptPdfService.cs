using System.Globalization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KasseAPI_Final.Services;

/// <summary>
/// RKSV-safe receipt reprint: uses persisted receipt number, TSE payload/signature snapshot, and stored QR string only.
/// </summary>
public sealed class ReceiptPdfService : IReceiptPdfService
{
    public const string ReprintWatermarkPrimary = "NACHAUSDRUCK - kein Original";

    private static readonly Color WatermarkGrayTranslucent = Color.FromARGB(77, 180, 0, 0);
    private static readonly Color BannerRed = Color.FromHex("#B71C1C");

    private readonly AppDbContext _context;
    private readonly IQrImageService _qrService;
    private readonly ISettingsTenantResolver _tenantResolver;

    static ReceiptPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public ReceiptPdfService(
        AppDbContext context,
        IQrImageService qrService,
        ISettingsTenantResolver tenantResolver)
    {
        _context = context;
        _qrService = qrService;
        _tenantResolver = tenantResolver;
    }

    /// <inheritdoc />
    public async Task<byte[]> GenerateReprintPdfAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);

        var receipt = await _context.Receipts.AsNoTracking()
            .Include(r => r.Items)
            .Include(r => r.TaxLines)
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
        byte[]? qrPng = null;
        if (!string.IsNullOrEmpty(qrPayload))
            qrPng = _qrService.GetQrPngFromExactPayload(qrPayload);

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
                page.Size(PageSizes.A6);
                page.Margin(1, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Background().Element(c => RenderDiagonalWatermark(c));

                page.Header().Column(h =>
                {
                    h.Item().AlignCenter().Text(ReprintWatermarkPrimary)
                        .FontSize(9)
                        .Italic()
                        .Bold()
                        .FontColor(BannerRed);
                    h.Item().PaddingTop(2).AlignCenter().Text("NACHAUSDRUCK")
                        .FontSize(28)
                        .Bold()
                        .FontColor(WatermarkGrayTranslucent);
                });

                page.Content().Column(column =>
                {
                    column.Spacing(4);

                    column.Item().Text($"Kassen-ID: {registerLabel}");
                    column.Item().Text($"Beleg-Nr: {receipt.ReceiptNumber}");
                    column.Item().Text($"Datum: {belegZeit}");
                    if (!string.IsNullOrEmpty(specialKind))
                        column.Item().Text($"Sonderbeleg: {specialKind}").SemiBold();

                    column.Item().LineHorizontal(0.75f).LineColor(Colors.Grey.Medium);

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellHeader).Text("Artikel");
                            header.Cell().Element(CellHeader).AlignRight().Text("Menge");
                            header.Cell().Element(CellHeader).AlignRight().Text("Preis");
                        });

                        foreach (var item in orderedItems)
                        {
                            table.Cell().Text(item.ProductName);
                            table.Cell().AlignRight().Text(item.Quantity.ToString(inv));
                            table.Cell().AlignRight().Text($"€{item.TotalPrice.ToString("F2", inv)}");
                        }
                    });

                    if (receipt.TaxLines.Count > 0)
                    {
                        column.Item().PaddingTop(4).Text("MwSt.").SemiBold().FontSize(8);
                        column.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                            });
                            t.Header(h =>
                            {
                                h.Cell().Element(CellHeader).Text("%");
                                h.Cell().Element(CellHeader).AlignRight().Text("Netto");
                                h.Cell().Element(CellHeader).AlignRight().Text("Steuer");
                                h.Cell().Element(CellHeader).AlignRight().Text("Brutto");
                            });
                            foreach (var tl in receipt.TaxLines.OrderBy(x => x.TaxRate))
                            {
                                t.Cell().Text(tl.TaxRate.ToString("F0", inv));
                                t.Cell().AlignRight().Text($"€{tl.NetAmount.ToString("F2", inv)}");
                                t.Cell().AlignRight().Text($"€{tl.TaxAmount.ToString("F2", inv)}");
                                t.Cell().AlignRight().Text($"€{tl.GrossAmount.ToString("F2", inv)}");
                            }
                        });
                    }

                    column.Item().LineHorizontal(0.75f).LineColor(Colors.Grey.Medium);
                    column.Item().AlignRight().Text($"Gesamt: €{payment.TotalAmount.ToString("F2", inv)}").SemiBold();
                    column.Item().AlignRight().Text($"Zahlung: {paymentMethodLabel}");

                    column.Item().PaddingTop(6).Text("TSE-Signatur (Auszug)").FontSize(7).FontColor(Colors.Grey.Darken2);
                    column.Item().Text(TruncateSignature(payment.TseSignature, 400)).FontSize(6).LineHeight(1.1f);

                    column.Item().PaddingTop(8);
                    if (qrPng != null && qrPng.Length > 0)
                    {
                        column.Item().Width(120).Height(120).Image(qrPng);
                    }
                    else if (!string.IsNullOrEmpty(qrPayload))
                    {
                        column.Item().Text("QR-Code konnte aus gespeicherten Daten nicht erzeugt werden.")
                            .FontSize(7)
                            .Italic()
                            .FontColor(Colors.Orange.Darken2);
                    }
                });

                page.Footer().AlignCenter().Text(
                        $"Erstellt: {FormatAustriaLocal(DateTime.UtcNow)} — Nachdruck (kein Original)")
                    .FontSize(7)
                    .FontColor(Colors.Grey.Darken1);
            });
        }).GeneratePdf();
    }

    private static IContainer CellHeader(IContainer c) =>
        c.DefaultTextStyle(x => x.SemiBold().FontSize(8)).PaddingVertical(2);

    private static void RenderDiagonalWatermark(IContainer container)
    {
        container.Extend().AlignCenter().AlignMiddle().Element(layer =>
            layer.Rotate(-32)
                .Text(ReprintWatermarkPrimary)
                .FontSize(22)
                .SemiBold()
                .FontColor(WatermarkGrayTranslucent)
                .AlignCenter());
    }

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
            return "—";
        return signature.Length <= maxChars ? signature : signature[..maxChars] + "…";
    }
}
