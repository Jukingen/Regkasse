using System.Globalization;
using KasseAPI_Final.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KasseAPI_Final.Services;

public static class FiskalyStatisticsPdfGenerator
{
    private const int MaxTableRows = 120;

    static FiskalyStatisticsPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Generate(FiskalyStatisticsDto stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        var inv = CultureInfo.InvariantCulture;
        var kpis = stats.Kpis;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.DefaultTextStyle(x => x.FontSize(10));
                page.Header().Text("Fiskaly statistics").SemiBold().FontSize(16);
                page.Footer().AlignRight().Text(text =>
                {
                    text.Span("Generated UTC ");
                    text.Span(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", inv));
                    text.Span(" · page ");
                    text.CurrentPageNumber();
                });
                page.Content().Column(col =>
                {
                    col.Spacing(10);
                    col.Item().Text($"Period: {stats.FromUtc:yyyy-MM-dd HH:mm}Z – {stats.ToUtc:yyyy-MM-dd HH:mm}Z");
                    if (stats.TenantId is { } tenantId)
                        col.Item().Text($"Tenant: {tenantId:D}");

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn();
                            c.RelativeColumn();
                        });
                        Cell(table, "Total operations", kpis.TotalOperations.ToString(inv));
                        Cell(table, "Success rate", $"{kpis.SuccessRatePercent.ToString("0.##", inv)} %");
                        Cell(table, "Most used type", kpis.MostUsedOperationType ?? "—");
                        Cell(table, "Avg. processing (ms)", kpis.AverageProcessingTimeMs?.ToString("0.#", inv) ?? "—");
                        Cell(table, "Errors", kpis.TotalErrors.ToString(inv));
                        Cell(table, "Success / failed", $"{kpis.SuccessCount} / {kpis.FailedCount}");
                    });

                    col.Item().Text("Operation types").SemiBold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn();
                            c.ConstantColumn(80);
                        });
                        Header(table, "Type");
                        Header(table, "Count");
                        foreach (var row in stats.ByOperationType.Take(MaxTableRows))
                        {
                            table.Cell().Text(row.Key);
                            table.Cell().Text(row.Count.ToString(inv));
                        }
                    });

                    col.Item().Text("Daily operations").SemiBold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn();
                            c.ConstantColumn(50);
                            c.ConstantColumn(50);
                            c.ConstantColumn(50);
                        });
                        Header(table, "Date");
                        Header(table, "Total");
                        Header(table, "OK");
                        Header(table, "Fail");
                        foreach (var row in stats.Daily.Take(MaxTableRows))
                        {
                            table.Cell().Text(row.Date);
                            table.Cell().Text(row.Total.ToString(inv));
                            table.Cell().Text(row.Success.ToString(inv));
                            table.Cell().Text(row.Failed.ToString(inv));
                        }
                    });
                });
            });
        }).GeneratePdf();
    }

    private static void Header(TableDescriptor table, string text) =>
        table.Cell().Background(Colors.Grey.Lighten3).Padding(2).Text(text).SemiBold();

    private static void Cell(TableDescriptor table, string label, string value)
    {
        table.Cell().Padding(2).Text(label);
        table.Cell().Padding(2).Text(value);
    }
}
