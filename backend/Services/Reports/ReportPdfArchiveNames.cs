namespace KasseAPI_Final.Services.Reports;

internal static class ReportPdfArchiveNames
{
    public static string ForReport(string reportType, Guid reportId) =>
        $"{reportType}_{reportId:N}_{DateTime.UtcNow:yyyyMMdd}.pdf";
}
