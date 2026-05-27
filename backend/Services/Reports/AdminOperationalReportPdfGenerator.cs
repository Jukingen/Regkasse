using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KasseAPI_Final.Services.Reports;

public static class AdminOperationalReportPdfGenerator
{
    static AdminOperationalReportPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Generate(string companyName, string reportTitle, IReadOnlyList<(string Label, string Value)> rows)
    {
        var inv = CultureInfo.InvariantCulture;
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));
                page.Header().Column(h =>
                {
                    h.Item().Text(companyName).Bold().FontSize(16);
                    h.Item().Text(reportTitle).SemiBold().FontSize(12);
                    h.Item().PaddingTop(4).Text($"Generated: {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", inv)} UTC")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                });
                page.Content().PaddingTop(12).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2);
                        c.RelativeColumn(3);
                    });
                    foreach (var (label, value) in rows)
                    {
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(label);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(value);
                    }
                });
                page.Footer().AlignCenter().Text("Regkasse — internal operational report").FontSize(8);
            });
        }).GeneratePdf();
    }
}
