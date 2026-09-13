using System.Globalization;
using System.Text;
using KasseAPI_Final.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KasseAPI_Final.Services.RestoreVerification;

public sealed class RestoreVerificationReportService : IRestoreVerificationReportService
{
    private readonly IRestoreVerificationRunQueryService _query;

    static RestoreVerificationReportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public RestoreVerificationReportService(IRestoreVerificationRunQueryService query)
    {
        _query = query;
    }

    public async Task<RestoreVerificationReportDto?> GetReportAsync(
        Guid runId,
        RestoreVerificationAccessScope access,
        CancellationToken cancellationToken = default)
    {
        var run = await _query.GetByIdAsync(runId, access, cancellationToken);
        if (run is null)
            return null;

        var snapshot = RestoreVerificationVerdictEvaluator.Evaluate(run);
        return new RestoreVerificationReportDto
        {
            Run = RestoreVerificationRunMapper.ToDto(run),
            Verdict = snapshot.Verdict,
            Checks = snapshot.Checks,
            FailedCheckIds = snapshot.FailedCheckIds,
            RowCounts = snapshot.RowCounts,
            FiscalSqlResult = snapshot.FiscalSqlResult,
            VerifiedAtUtc = snapshot.VerifiedAtUtc
        };
    }

    public byte[] ToCsv(RestoreVerificationReportDto report)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("section,key,value");
        Csv(sb, "run", "id", report.Run.Id.ToString("D"));
        Csv(sb, "run", "requestedAtUtc", Iso(report.Run.RequestedAt));
        Csv(sb, "run", "completedAtUtc", report.Run.CompletedAt is { } c ? Iso(c) : "");
        Csv(sb, "run", "sourceBackupRunId", report.Run.SourceBackupRunId?.ToString("D") ?? "");
        Csv(sb, "run", "type", report.Run.TriggerSource.ToString());
        Csv(sb, "run", "status", report.Run.Status.ToString());
        Csv(sb, "run", "verdict", report.Verdict);
        Csv(sb, "run", "verifiedAtUtc", report.VerifiedAtUtc is { } v ? Iso(v) : "");
        Csv(sb, "run", "fiscalSqlResult", report.FiscalSqlResult ?? "");
        Csv(sb, "run", "failedCheckIds", string.Join('|', report.FailedCheckIds));

        foreach (var check in report.Checks)
        {
            Csv(sb, "check", check.Id, $"{check.Result}|{check.Detail}");
        }

        foreach (var row in report.RowCounts)
        {
            Csv(
                sb,
                "rowCount",
                row.Id,
                string.Create(
                    inv,
                    $"{row.Name}|{row.Category}|measured={row.Measured}|min={row.ExpectedAtLeast}|{row.Status}"));
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    public byte[] ToPdf(RestoreVerificationReportDto report)
    {
        var inv = CultureInfo.InvariantCulture;
        var verdictColor = report.Verdict switch
        {
            RestoreVerificationVerdictEvaluator.VerdictPassed => Colors.Green.Darken2,
            RestoreVerificationVerdictEvaluator.VerdictFailed => Colors.Red.Darken2,
            _ => Colors.Orange.Darken2
        };

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9));
                page.Header().Column(h =>
                {
                    h.Item().Text("Regkasse — Restore verification report").Bold().FontSize(16);
                    h.Item().Text($"Run {report.Run.Id:D}").FontSize(10);
                    h.Item().PaddingTop(2)
                        .Text($"Generated {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", inv)} UTC")
                        .FontSize(8)
                        .FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    col.Item().Text(report.Verdict.ToUpperInvariant())
                        .Bold()
                        .FontSize(18)
                        .FontColor(verdictColor);
                    col.Item().PaddingBottom(8).Text(report.FiscalSqlResult ?? "").FontSize(10);

                    col.Item().Text("Summary").SemiBold().FontSize(12);
                    col.Item().PaddingBottom(8).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn(3);
                        });
                        AddRow(table, "Status", report.Run.Status.ToString());
                        AddRow(table, "Type", report.Run.TriggerSource.ToString());
                        AddRow(table, "Backup ID", report.Run.SourceBackupRunId?.ToString("D") ?? "—");
                        AddRow(table, "Requested", Iso(report.Run.RequestedAt));
                        AddRow(table, "Verified", report.VerifiedAtUtc is { } v ? Iso(v) : "—");
                        AddRow(table, "Failed checks", report.FailedCheckIds.Count == 0
                            ? "none"
                            : string.Join(", ", report.FailedCheckIds));
                    });

                    col.Item().Text("Verification checks").SemiBold().FontSize(12);
                    col.Item().PaddingBottom(8).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(4);
                        });
                        table.Header(header =>
                        {
                            header.Cell().Element(CellHeader).Text("Check");
                            header.Cell().Element(CellHeader).Text("Result");
                            header.Cell().Element(CellHeader).Text("Detail");
                        });
                        foreach (var check in report.Checks)
                        {
                            table.Cell().Element(CellBody).Text(check.Id);
                            table.Cell().Element(CellBody).Text(check.Result.ToUpperInvariant());
                            table.Cell().Element(CellBody).Text(check.Detail ?? "");
                        }
                    });

                    if (report.RowCounts.Count > 0)
                    {
                        col.Item().Text("Row counts").SemiBold().FontSize(12);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(2);
                                c.RelativeColumn(2);
                                c.RelativeColumn(1);
                                c.RelativeColumn(1);
                                c.RelativeColumn(1);
                            });
                            table.Header(header =>
                            {
                                header.Cell().Element(CellHeader).Text("Id");
                                header.Cell().Element(CellHeader).Text("Name");
                                header.Cell().Element(CellHeader).Text("Measured");
                                header.Cell().Element(CellHeader).Text("Min");
                                header.Cell().Element(CellHeader).Text("Status");
                            });
                            foreach (var row in report.RowCounts)
                            {
                                table.Cell().Element(CellBody).Text(row.Id);
                                table.Cell().Element(CellBody).Text(row.Name);
                                table.Cell().Element(CellBody).Text(row.Measured?.ToString(inv) ?? "—");
                                table.Cell().Element(CellBody).Text(row.ExpectedAtLeast?.ToString(inv) ?? "—");
                                table.Cell().Element(CellBody).Text(row.Status);
                            }
                        });
                    }
                });
            });
        }).GeneratePdf();
    }

    private static void Csv(StringBuilder sb, string section, string key, string? value)
    {
        sb.Append(section)
            .Append(',')
            .Append(Escape(key))
            .Append(',')
            .Append(Escape(value))
            .AppendLine();
    }

    private static string Escape(string? value)
    {
        var v = value ?? "";
        if (v.Contains('"') || v.Contains(',') || v.Contains('\n') || v.Contains('\r'))
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        return v;
    }

    private static string Iso(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static void AddRow(TableDescriptor table, string label, string value)
    {
        table.Cell().Element(CellBody).Text(label);
        table.Cell().Element(CellBody).Text(value);
    }

    private static IContainer CellHeader(IContainer c) =>
        c.BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(3).DefaultTextStyle(x => x.SemiBold());

    private static IContainer CellBody(IContainer c) =>
        c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3);
}
