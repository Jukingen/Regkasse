using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Services.Reports;

/// <summary>Archive / save-time report PDF names (via <see cref="IFileNamingService"/>).</summary>
internal static class ReportPdfArchiveNames
{
    public static string ForReport(
        string reportType,
        string? tenantSlug,
        DateTime businessDate,
        DateTime? generatedAt = null)
    {
        var type = ReportExportFileNames.NormalizeReportTypeLabel(reportType);
        var period = ReportExportFileNames.PeriodForReportType(reportType, businessDate);
        return new FileNamingService(NullCurrentTenantAccessor.Instance).GenerateFileName(
            $"{ReportExportFileNames.Prefix}_{type}",
            "pdf",
            additional: period,
            at: generatedAt,
            tenantSlug: tenantSlug);
    }

    /// <summary>Legacy overload when tenant/period are unknown — uses report id fragment as period.</summary>
    public static string ForReport(string reportType, Guid reportId)
    {
        var type = ReportExportFileNames.NormalizeReportTypeLabel(reportType);
        return new FileNamingService(NullCurrentTenantAccessor.Instance).GenerateFileName(
            $"{ReportExportFileNames.Prefix}_{type}",
            "pdf",
            additional: reportId.ToString("N")[..8]);
    }
}
