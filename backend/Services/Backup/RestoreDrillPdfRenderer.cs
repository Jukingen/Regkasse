using System.Globalization;
using System.Text.RegularExpressions;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.RestoreVerification;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KasseAPI_Final.Services.Backup;

/// <summary>Single-drill A4 report. Does not read RKSV/TSE/DEP payloads and does not persist PDF bytes.</summary>
public sealed class RestoreDrillPdfRenderer
{
    public const string NotReportableCode = "DRILL_NOT_REPORTABLE";

    static RestoreDrillPdfRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(RestoreVerificationRun run, string operatorUserId, DateTime generatedAtUtc)
    {
        if (!IsReportable(run))
            throw new RestoreDrillNotReportableException();

        var badge = run.Status == RestoreVerificationStatus.Succeeded ? "Passed" : "Failed";
        var snapshot = RestoreVerificationVerdictEvaluator.Evaluate(run);
        var lines = NarrativeLines(run, snapshot.FailedCheckIds).ToArray();
        var inv = CultureInfo.InvariantCulture;
        var generated = generatedAtUtc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss", inv);
        var actor = string.IsNullOrWhiteSpace(operatorUserId) ? "unknown" : operatorUserId.Trim();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.4f, Unit.Centimetre);
                page.DefaultTextStyle(style => style.FontSize(9));

                page.Header().Row(row =>
                {
                    row.ConstantItem(42).Height(42).Border(1).BorderColor(Colors.Grey.Medium)
                        .AlignCenter().AlignMiddle()
                        .Text("RK").Bold().FontSize(14);
                    row.RelativeItem().PaddingLeft(10).Column(col =>
                    {
                        col.Item().Text("Restore-Drill").Bold().FontSize(16);
                        col.Item().Text(run.Id.ToString("D")).FontSize(8).FontColor(Colors.Grey.Darken2);
                    });
                });

                page.Content().PaddingTop(12).Column(col =>
                {
                    col.Item().Text($"{badge}  {Iso(run.CompletedAt ?? run.RequestedAt)}")
                        .Bold().FontSize(14)
                        .FontColor(badge == "Passed" ? Colors.Green.Darken2 : Colors.Red.Darken2);

                    col.Item().PaddingTop(8).Text("Source backup").SemiBold();
                    col.Item().Text($"Id: {run.SourceBackupRunId?.ToString("D") ?? "—"}");
                    col.Item().Text($"Created: {Iso(run.SourceBackupRun?.RequestedAt)}");

                    col.Item().PaddingTop(6).Text("Restore").SemiBold();
                    col.Item().Text($"Duration: {FormatDuration(run.DurationMs)}");

                    col.Item().PaddingTop(8).Text("Integrity checks").SemiBold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(4);
                        });
                        table.Header(header =>
                        {
                            header.Cell().Text("Check").SemiBold();
                            header.Cell().Text("Result").SemiBold();
                            header.Cell().Text("Detail").SemiBold();
                        });
                        foreach (var check in snapshot.Checks)
                        {
                            table.Cell().Text(check.Id);
                            table.Cell().Text(check.Result);
                            table.Cell().Text(Short(check.Detail));
                        }
                    });

                    col.Item().PaddingTop(8).Text("Warnings").SemiBold();
                    foreach (var warning in Warnings(run))
                        col.Item().Text(warning);

                    col.Item().PaddingTop(6).Text("Errors").SemiBold();
                    foreach (var error in lines)
                        col.Item().Text(error);
                });

                page.Footer().Column(col =>
                {
                    col.Item().Text($"Generated {generated} UTC · {actor}").FontSize(8);
                    col.Item().Text("Dies ist ein interner Prüfbericht, kein RKSV-Beleg.").FontSize(8);
                });
            });
        }).GeneratePdf();
    }

    public static bool IsReportable(RestoreVerificationRun run)
    {
        if (run.FailureCategory == RestoreDrillFailureCategory.CancelledOrTimeout)
            return false;
        if (string.Equals(run.FailureCode, "CANCELLED", StringComparison.OrdinalIgnoreCase))
            return false;
        return run.Status is RestoreVerificationStatus.Succeeded or RestoreVerificationStatus.Failed;
    }

    private static IEnumerable<string> Warnings(RestoreVerificationRun run)
    {
        var any = false;
        if (run.FiscalSqlWarnCount is > 0)
        {
            any = true;
            yield return $"Fiscal warnings: {run.FiscalSqlWarnCount.Value.ToString(CultureInfo.InvariantCulture)}";
        }

        if (!string.IsNullOrWhiteSpace(run.FiscalSqlSkipReason))
        {
            any = true;
            yield return $"Fiscal skip: {Short(run.FiscalSqlSkipReason)}";
        }

        if (!string.IsNullOrWhiteSpace(run.RestoreAttemptSkipReason))
        {
            any = true;
            yield return $"Restore skip: {Short(run.RestoreAttemptSkipReason)}";
        }

        if (!any)
            yield return "—";
    }

    internal static IEnumerable<string> NarrativeLines(
        RestoreVerificationRun run,
        IReadOnlyList<string> failedCheckIds) =>
        Errors(run, failedCheckIds);

    private static IEnumerable<string> Errors(
        RestoreVerificationRun run,
        IReadOnlyList<string> failedCheckIds)
    {
        var any = false;
        if (run.Status == RestoreVerificationStatus.Failed)
        {
            any = true;
            var code = string.IsNullOrWhiteSpace(run.FailureCode) ? "FAILED" : run.FailureCode.Trim();
            yield return $"{code}: {Short(run.FailureDetail)}";
        }

        if (failedCheckIds.Count > 0)
        {
            any = true;
            yield return "Failed checks: " + string.Join(", ", failedCheckIds);
        }

        if (!any)
            yield return "—";
    }

    internal static string Short(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "—";

        var text = Regex.Replace(value, @"\s+", " ").Trim();
        if (LooksSensitive(text))
            return "***";

        return text.Length <= 180 ? text : text[..177] + "…";
    }

    private static bool LooksSensitive(string text)
    {
        return text.Contains("password", StringComparison.OrdinalIgnoreCase)
               || text.Contains("secret", StringComparison.OrdinalIgnoreCase)
               || text.Contains("credential", StringComparison.OrdinalIgnoreCase)
               || text.Contains("Host=", StringComparison.OrdinalIgnoreCase)
               || text.Contains("://", StringComparison.Ordinal)
               || text.Contains('\\')
               || text.Contains(".cs:", StringComparison.Ordinal);
    }

    private static string FormatDuration(long? durationMs)
    {
        if (durationMs is not long ms || ms < 0)
            return "—";
        var seconds = ms / 1000d;
        return seconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";
    }

    private static string Iso(DateTime? value) =>
        value.HasValue
            ? value.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC"
            : "—";
}

public sealed class RestoreDrillNotReportableException : InvalidOperationException
{
    public RestoreDrillNotReportableException()
        : base(RestoreDrillPdfRenderer.NotReportableCode)
    {
    }
}
