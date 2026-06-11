using System.Globalization;
using KasseAPI_Final.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KasseAPI_Final.Services.Reports;

public static class DailyClosingReportPdfGenerator
{
    /// <summary>Thermal roll width (~80 mm).</summary>
    private const float ThermalRollWidthPoints = 288f;

    private const float PageMarginPoints = 8f;
    private const int TseSignaturePreviewMaxChars = 48;

    static DailyClosingReportPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Generate(
        PosDailyClosingReportDto report,
        DailyClosingReportLabels labels,
        CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(culture);

        var dateText = report.BusinessDate.ToString("dd.MM.yyyy", culture);
        var register = string.IsNullOrWhiteSpace(report.RegisterNumber) ? "—" : report.RegisterNumber.Trim();
        var tsePreview = FormatTsePreview(report.TseSignature);
        var disclaimer = culture.TwoLetterISOLanguageName == "de" &&
                         !string.IsNullOrWhiteSpace(report.SnapshotDisclaimerDe)
            ? report.SnapshotDisclaimerDe
            : labels.Disclaimer;

        var rows = new (string Label, string Value)[]
        {
            (labels.Date, dateText),
            (labels.Register, register),
            (labels.TotalSales, FormatMoney(report.TotalSales, culture)),
            (labels.TotalCash, FormatMoney(report.TotalCash, culture)),
            (labels.TotalCard, FormatMoney(report.TotalCard, culture)),
            (labels.CashCount, FormatMoney(report.CashCount, culture)),
            (labels.Difference, FormatMoney(report.Difference, culture)),
            (labels.FiscalTotal, FormatMoney(report.FiscalTotalAmount, culture)),
            (labels.FiscalTax, FormatMoney(report.FiscalTotalTaxAmount, culture)),
            (labels.Transactions, report.FiscalTransactionCount.ToString(culture)),
            (labels.TseSignature, tsePreview),
        };

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.ContinuousSize(ThermalRollWidthPoints);
                page.Margin(PageMarginPoints);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Content().Column(col =>
                {
                    col.Spacing(6);
                    col.Item().AlignCenter().Text(labels.Title).Bold().FontSize(12);
                    col.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn(2);
                        });

                        foreach (var (label, value) in rows)
                        {
                            table.Cell().PaddingVertical(2).Text(label).FontColor(Colors.Grey.Darken1);
                            table.Cell().PaddingVertical(2).AlignRight().Text(value).SemiBold();
                        }
                    });
                    col.Item().PaddingTop(8).Text(disclaimer).FontSize(7).FontColor(Colors.Grey.Darken1);
                });
            });
        }).GeneratePdf();
    }

    private static string FormatMoney(decimal amount, CultureInfo culture) =>
        amount.ToString("C", culture);

    private static string FormatTsePreview(string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
            return "—";

        var trimmed = signature.Trim();
        return trimmed.Length > TseSignaturePreviewMaxChars
            ? $"{trimmed[..TseSignaturePreviewMaxChars]}…"
            : trimmed;
    }
}
