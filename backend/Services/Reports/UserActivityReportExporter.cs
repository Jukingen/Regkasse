using System.Globalization;
using System.Text;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services.Reports;

namespace KasseAPI_Final.Services;

public static class UserActivityReportExporter
{
    public static (byte[] Content, string ContentType, string FileName) Export(
        UserActivityReportDto report,
        string format)
    {
        var normalized = (format ?? "csv").Trim().ToLowerInvariant();
        var stem = $"user-activity-{report.UserName}-{DateTime.UtcNow:yyyyMMddHHmmss}"
            .Replace('@', '-');

        if (normalized == "pdf")
        {
            var rows = BuildSummaryRows(report);
            foreach (var item in report.ActivityTimeline.Take(200))
            {
                rows.Add((
                    $"{item.Date:yyyy-MM-dd HH:mm:ss} {item.Action}",
                    $"{item.EntityType} | {item.Status} | IP {item.IpAddress ?? "—"}"));
            }

            var company = string.IsNullOrWhiteSpace(report.TenantName) ? "Regkasse" : report.TenantName;
            return (
                AdminOperationalReportPdfGenerator.Generate(
                    company,
                    $"User activity — {report.UserName}",
                    rows),
                "application/pdf",
                $"{stem}.pdf");
        }

        return (BuildCsv(report), "text/csv", $"{stem}.csv");
    }

    private static List<(string Label, string Value)> BuildSummaryRows(UserActivityReportDto r)
    {
        var inv = CultureInfo.InvariantCulture;
        var a = r.ActionsPerformed;
        return new List<(string, string)>
        {
            ("User", r.UserName),
            ("Email", r.Email),
            ("Role", r.Role),
            ("Tenant", r.TenantName),
            ("Period from (UTC)", r.FromDateUtc.ToString("yyyy-MM-dd HH:mm:ss", inv)),
            ("Period to (UTC)", r.ToDateUtc.ToString("yyyy-MM-dd HH:mm:ss", inv)),
            ("Total actions", r.TotalActions.ToString(inv)),
            ("Total logins", r.TotalLogins.ToString(inv)),
            ("Failed logins", r.FailedLoginAttempts.ToString(inv)),
            ("Last login (UTC)", r.LastLoginAt?.ToString("yyyy-MM-dd HH:mm:ss", inv) ?? "—"),
            ("Last login IP", r.LastLoginIp ?? "—"),
            ("Active sessions", r.ActiveSessions.ToString(inv)),
            ("Avg session (min)", r.AverageSessionDurationMinutes.ToString("F1", inv)),
            ("User creates", a.UserCreates.ToString(inv)),
            ("User edits", a.UserEdits.ToString(inv)),
            ("Payments", a.PaymentsProcessed.ToString(inv)),
            ("Stornos", a.Stornos.ToString(inv)),
            ("Refunds", a.Refunds.ToString(inv)),
            ("Exports", a.Exports.ToString(inv)),
        };
    }

    private static byte[] BuildCsv(UserActivityReportDto report)
    {
        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;
        sb.AppendLine("Section,Field,Value");
        foreach (var (label, value) in BuildSummaryRows(report))
            sb.AppendLine($"Summary,{EscapeCsv(label)},{EscapeCsv(value)}");

        sb.AppendLine();
        sb.AppendLine(
            "TimestampUtc,Action,EntityType,EntityId,Status,SessionId,CorrelationId,IpAddress,Description,TseSignature");
        foreach (var item in report.ActivityTimeline)
        {
            sb.AppendLine(string.Join(',',
                EscapeCsv(item.Date.ToString("yyyy-MM-dd HH:mm:ss", inv)),
                EscapeCsv(item.Action),
                EscapeCsv(item.EntityType),
                EscapeCsv(item.EntityId?.ToString() ?? ""),
                EscapeCsv(item.Status),
                EscapeCsv(item.SessionId ?? ""),
                EscapeCsv(item.CorrelationId ?? ""),
                EscapeCsv(item.IpAddress ?? ""),
                EscapeCsv(item.Description ?? ""),
                EscapeCsv(item.TseSignature ?? "")));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
