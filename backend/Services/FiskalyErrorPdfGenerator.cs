using System.Globalization;
using KasseAPI_Final.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KasseAPI_Final.Services;

public static class FiskalyErrorPdfGenerator
{
    private const int MaxTableRows = 80;

    static FiskalyErrorPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Generate(FiskalyErrorStatsDto stats, IReadOnlyList<FiskalyErrorListItemDto> items)
    {
        ArgumentNullException.ThrowIfNull(stats);
        ArgumentNullException.ThrowIfNull(items);
        var inv = CultureInfo.InvariantCulture;
        var kpis = stats.Kpis;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.DefaultTextStyle(x => x.FontSize(10));
                page.Header().Text("Fiskaly error analysis").SemiBold().FontSize(16);
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
                        Cell(table, "Total errors", kpis.TotalErrors.ToString(inv));
                        Cell(table, "Error rate", $"{kpis.ErrorRatePercent.ToString("0.##", inv)} %");
                        Cell(table, "Total operations", kpis.TotalOperations.ToString(inv));
                        Cell(table, "Most common error", $"{kpis.MostCommonErrorCode ?? "—"} ({kpis.MostCommonErrorCount})");
                        Cell(table, "Tenant with most errors", kpis.TenantWithMostErrorsName ?? kpis.TenantWithMostErrorsId?.ToString("D") ?? "—");
                        Cell(table, "Open / resolved / known", $"{kpis.OpenCount} / {kpis.ResolvedCount} / {kpis.KnownIssueCount}");
                    });

                    col.Item().Text("Errors by code").SemiBold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn();
                            c.ConstantColumn(80);
                        });
                        Header(table, "Code");
                        Header(table, "Count");
                        foreach (var row in stats.TopErrors.Take(MaxTableRows))
                        {
                            table.Cell().Text(row.Key);
                            table.Cell().Text(row.Count.ToString(inv));
                        }
                    });

                    col.Item().Text("Errors by operation type").SemiBold();
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

                    col.Item().Text("Daily errors").SemiBold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn();
                            c.ConstantColumn(80);
                        });
                        Header(table, "Date");
                        Header(table, "Count");
                        foreach (var row in stats.Daily.Take(MaxTableRows))
                        {
                            table.Cell().Text(row.Date);
                            table.Cell().Text(row.Count.ToString(inv));
                        }
                    });

                    col.Item().Text("Error list").SemiBold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn();
                            c.RelativeColumn(2);
                            c.RelativeColumn();
                        });
                        Header(table, "UTC");
                        Header(table, "Code");
                        Header(table, "Message");
                        Header(table, "Type");
                        foreach (var item in items.Take(MaxTableRows))
                        {
                            table.Cell().Text(item.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm", inv));
                            table.Cell().Text(item.ErrorCode ?? "—");
                            table.Cell().Text(Truncate(item.ErrorMessage, 80));
                            table.Cell().Text(item.OperationType);
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

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "—";
        return value.Length <= max ? value : value[..max] + "…";
    }
}
