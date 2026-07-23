using System.Globalization;
using KasseAPI_Final.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KasseAPI_Final.Services.Reports;

public static class PermissionAuditReportPdfGenerator
{
    static PermissionAuditReportPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Generate(
        PermissionAuditReportDto report,
        IReadOnlyList<PermissionAuditEntryDto> sampleEntries)
    {
        var inv = CultureInfo.InvariantCulture;
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9));
                page.Header().Column(h =>
                {
                    h.Item().Text("Regkasse — Permission Audit Report").Bold().FontSize(16);
                    h.Item().Text(
                            $"Period: {report.FromUtc.ToString("yyyy-MM-dd", inv)} → {report.ToUtc.ToString("yyyy-MM-dd", inv)} UTC")
                        .FontSize(10);
                    h.Item().PaddingTop(2).Text($"Generated: {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", inv)} UTC")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    col.Item().Text("Summary").SemiBold().FontSize(12);
                    col.Item().PaddingBottom(8).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn(3);
                        });
                        AddRow(table, "Total changes", report.TotalChanges.ToString(inv));
                        AddRow(table, "Critical changes", report.CriticalCount.ToString(inv));
                        AddRow(table, "Unique actors", report.UniqueActors.ToString(inv));
                        AddRow(table, "Unique permissions", report.UniquePermissions.ToString(inv));
                    });

                    col.Item().PaddingTop(6).Text("Most active users").SemiBold().FontSize(11);
                    col.Item().PaddingBottom(6).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn(1);
                        });
                        foreach (var row in report.TopActors.Take(8))
                            AddRow(table, row.Label, row.Count.ToString(inv));
                    });

                    col.Item().PaddingTop(6).Text("Most frequently changed permissions").SemiBold().FontSize(11);
                    col.Item().PaddingBottom(6).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn(1);
                        });
                        foreach (var row in report.TopPermissions.Take(10))
                            AddRow(table, row.Label, row.Count.ToString(inv));
                    });

                    col.Item().PaddingTop(6).Text("Permission changes by date").SemiBold().FontSize(11);
                    col.Item().PaddingBottom(6).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn(1);
                        });
                        foreach (var row in report.ByDate.Take(31))
                            AddRow(table, row.Date, row.Count.ToString(inv));
                    });

                    if (sampleEntries.Count > 0)
                    {
                        col.Item().PaddingTop(8).Text("Sample entries (max 200)").SemiBold().FontSize(11);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(2);
                                c.RelativeColumn(1);
                                c.RelativeColumn(2);
                                c.RelativeColumn(2);
                            });
                            table.Header(h =>
                            {
                                h.Cell().Text("When").SemiBold();
                                h.Cell().Text("Action").SemiBold();
                                h.Cell().Text("Actor").SemiBold();
                                h.Cell().Text("Permission").SemiBold();
                            });
                            foreach (var e in sampleEntries)
                            {
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2)
                                    .Text(e.Timestamp.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", inv)).FontSize(7);
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2)
                                    .Text(e.Action).FontSize(7);
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2)
                                    .Text(e.ActorName).FontSize(7);
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2)
                                    .Text(string.IsNullOrWhiteSpace(e.PermissionKey) ? e.RoleName : e.PermissionKey)
                                    .FontSize(7);
                            }
                        });
                    }
                });

                page.Footer().AlignCenter()
                    .Text("Regkasse — confidential permission audit (for auditors)")
                    .FontSize(8);
            });
        }).GeneratePdf();
    }

    private static void AddRow(TableDescriptor table, string label, string value)
    {
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(label);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(value);
    }
}
