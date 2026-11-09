using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KasseAPI_Final.Services.Reports;

/// <summary>A4 sales report used by Tagesbericht and Jahresbericht. No RKSV QR or signature block.</summary>
internal static class OperationalPeriodReportPdf
{
    static OperationalPeriodReportPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Generate(
        string title,
        string documentNumber,
        string? registerLabel,
        PeriodSalesSummary sales)
    {
        var heading = string.IsNullOrWhiteSpace(title) ? "Bericht" : title.Trim();
        var number = string.IsNullOrWhiteSpace(documentNumber) ? "—" : documentNumber.Trim();
        var register = string.IsNullOrWhiteSpace(registerLabel) ? "—" : registerLabel.Trim();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Content().Column(column =>
                {
                    column.Spacing(4);
                    column.Item().Text(heading).Bold().FontSize(18);
                    column.Item().Text($"Bericht-Nr. {number}").Bold().FontSize(12);
                    column.Item().Text(sales.PeriodLabel).SemiBold();
                    column.Item().Text($"Kasse: {register}").FontSize(10).FontColor(Colors.Grey.Darken2);

                    PeriodSalesPdfSections.Write(column, sales);

                    column.Item().PaddingTop(16)
                        .Text("Umsatzübersicht aus den Zahlungszeilen des Berichtszeitraums. Die RKSV-Signatur und der QR-Code des zugehörigen Sonderbelegs werden hier nicht verändert.")
                        .FontSize(8)
                        .FontColor(Colors.Grey.Darken1)
                        .LineHeight(1.3f);
                });
            });
        }).GeneratePdf();
    }
}
