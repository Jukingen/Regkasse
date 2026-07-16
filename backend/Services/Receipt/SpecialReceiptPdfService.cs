using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KasseAPI_Final.Services;

/// <summary>
/// Input model for RKSV Sonderbeleg PDF export (Start-/Null-/Monats-/Jahres-/Schlussbeleg).
/// Company fields come from payment snapshot / <see cref="Models.CompanySettings"/> (UID = CompanyTaxNumber).
/// </summary>
public sealed class SpecialReceiptPdfData
{
    public Guid TenantId { get; init; }

    public string CompanyName { get; init; } = string.Empty;

    public string CompanyAddress { get; init; } = string.Empty;

    /// <summary>UID (ATU…); mapped from <see cref="Models.CompanySettings.CompanyTaxNumber"/> / payment Steuernummer.</summary>
    public string CompanyVatId { get; init; } = string.Empty;

    public string ReceiptType { get; init; } = string.Empty;

    public string CashRegisterId { get; init; } = string.Empty;

    public string? RegisterNumber { get; init; }

    public string ReceiptNumber { get; init; } = string.Empty;

    public DateTime IssuedAt { get; init; }

    public decimal TotalAmount { get; init; }

    public string PaymentMethod { get; init; } = "—";

    public string? TseSignature { get; init; }

    public string? TseSignatureTimestamp { get; init; }

    /// <summary>Persisted RKSV QR machine-code / payload used to embed the QR image.</summary>
    public string QrContent { get; init; } = string.Empty;

    /// <summary>Short RKSV compliance label (e.g. RKSV-konform or DEMO / NICHT FISKAL).</summary>
    public string RksvFooterLabel { get; init; } = "RKSV-konform";

    public bool IncludeReprintWatermark { get; init; }
}

/// <summary>
/// QuestPDF layout for RKSV special receipts: company header, UID, QR, full TSE signature, RKSV footer.
/// </summary>
public static class SpecialReceiptPdfService
{
    private const float ThermalRollWidthPoints = 288f;
    private const float PageMarginPoints = 6f;
    private const float QrSizePoints = 160f;

    static SpecialReceiptPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Generate(SpecialReceiptPdfData data, byte[]? qrPng = null, string? qrFallbackText = null)
    {
        ArgumentNullException.ThrowIfNull(data);

        var deAt = CultureInfo.GetCultureInfo("de-AT");
        var companyName = string.IsNullOrWhiteSpace(data.CompanyName) ? "—" : data.CompanyName.Trim();
        var companyAddress = string.IsNullOrWhiteSpace(data.CompanyAddress) ? "—" : data.CompanyAddress.Trim();
        var companyVatId = string.IsNullOrWhiteSpace(data.CompanyVatId) ? "—" : data.CompanyVatId.Trim();
        var receiptType = string.IsNullOrWhiteSpace(data.ReceiptType) ? "Sonderbeleg" : data.ReceiptType.Trim();
        var kassenId = !string.IsNullOrWhiteSpace(data.RegisterNumber)
            ? data.RegisterNumber.Trim()
            : (string.IsNullOrWhiteSpace(data.CashRegisterId) ? "—" : data.CashRegisterId.Trim());
        var receiptNumber = string.IsNullOrWhiteSpace(data.ReceiptNumber) ? "—" : data.ReceiptNumber.Trim();
        var issuedLocal = FormatAustriaLocal(data.IssuedAt);
        var paymentMethod = string.IsNullOrWhiteSpace(data.PaymentMethod) ? "—" : data.PaymentMethod.Trim();
        var tseSignature = string.IsNullOrWhiteSpace(data.TseSignature) ? "—" : data.TseSignature.Trim();
        var footerLabel = string.IsNullOrWhiteSpace(data.RksvFooterLabel)
            ? "RKSV-konform"
            : data.RksvFooterLabel.Trim();
        var hasQrImage = qrPng is { Length: > 0 };
        var qrFallback = !string.IsNullOrWhiteSpace(qrFallbackText)
            ? qrFallbackText.Trim()
            : data.QrContent?.Trim();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.ContinuousSize(ThermalRollWidthPoints);
                page.Margin(PageMarginPoints);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Content().Column(column =>
                {
                    column.Spacing(4);

                    if (data.IncludeReprintWatermark)
                    {
                        column.Item().AlignCenter().Text(ReceiptPdfService.ReprintWatermarkPrimary)
                            .FontSize(8).Italic().FontColor(Colors.Grey.Darken2);
                        column.Item().AlignCenter().Text("(kein Original)")
                            .FontSize(7).Italic().FontColor(Colors.Grey.Darken2);
                    }

                    column.Item().AlignCenter().Text(companyName).Bold().FontSize(12);
                    column.Item().AlignCenter().Text(companyAddress).FontSize(8).FontColor(Colors.Grey.Darken2);
                    column.Item().AlignCenter().Text($"UID: {companyVatId}").FontSize(8).SemiBold();

                    column.Item().PaddingTop(2).AlignCenter().Text(receiptType.ToUpperInvariant()).Bold().FontSize(13);
                    column.Item().AlignCenter().Text(footerLabel).FontSize(9).SemiBold()
                        .FontColor(Colors.Grey.Darken2);

                    column.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn(2);
                        });

                        void Row(string label, string value)
                        {
                            table.Cell().PaddingVertical(2).Text(label).FontColor(Colors.Grey.Darken1);
                            table.Cell().PaddingVertical(2).AlignRight().Text(value).SemiBold();
                        }

                        Row("Kassen-ID", kassenId);
                        if (!string.IsNullOrWhiteSpace(data.RegisterNumber)
                            && !string.IsNullOrWhiteSpace(data.CashRegisterId)
                            && !string.Equals(data.RegisterNumber.Trim(), data.CashRegisterId.Trim(), StringComparison.Ordinal))
                        {
                            Row("Kassen-UUID", data.CashRegisterId.Trim());
                        }

                        Row("Beleg-Nr.", receiptNumber);
                        Row("Datum", issuedLocal);
                        Row("Gesamtbetrag", $"€ {data.TotalAmount.ToString("F2", deAt)}");
                        Row("Zahlungsart", paymentMethod);
                    });

                    column.Item().PaddingTop(6).Text("TSE-Signatur").Bold().FontSize(8);
                    column.Item().Text(tseSignature).FontSize(6).FontFamily("Courier New");

                    if (!string.IsNullOrWhiteSpace(data.TseSignatureTimestamp))
                    {
                        column.Item().PaddingTop(2)
                            .Text($"Zeitstempel: {data.TseSignatureTimestamp.Trim()}")
                            .FontSize(7)
                            .FontColor(Colors.Grey.Darken1);
                    }

                    column.Item().PaddingTop(6).AlignCenter().Text("RKSV-QR-Code").FontSize(8).SemiBold();
                    if (hasQrImage)
                    {
                        column.Item().AlignCenter()
                            .Width(QrSizePoints)
                            .Height(QrSizePoints)
                            .Image(qrPng!)
                            .FitArea();
                    }
                    else if (!string.IsNullOrWhiteSpace(qrFallback))
                    {
                        column.Item().AlignCenter().Text("[QR-Daten]").FontSize(7).FontColor(Colors.Grey.Darken2);
                        foreach (var line in ChunkForThermal(qrFallback!, 26))
                            column.Item().AlignCenter().Text(line).FontSize(5).FontColor(Colors.Grey.Darken1);
                    }
                    else
                    {
                        column.Item().AlignCenter().Text("QR nicht verfügbar").FontSize(7)
                            .FontColor(Colors.Orange.Darken2);
                    }

                    column.Item().PaddingTop(8)
                        .Text("Registrierkassensicherheitsverordnung (RKSV)")
                        .FontSize(7)
                        .FontColor(Colors.Grey.Darken1)
                        .LineHeight(1.3f);
                    column.Item()
                        .Text("Dieser Beleg ist fiskalisch gültig.")
                        .FontSize(7)
                        .FontColor(Colors.Grey.Darken1);
                    column.Item()
                        .Text(footerLabel)
                        .FontSize(7)
                        .SemiBold()
                        .FontColor(Colors.Grey.Darken2);
                });
            });
        }).GeneratePdf();
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
}
