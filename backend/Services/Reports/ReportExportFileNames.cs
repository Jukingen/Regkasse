using KasseAPI_Final.Tenancy;



namespace KasseAPI_Final.Services.Reports;



/// <summary>

/// Canonical report download names:

/// <c>report_{reportType}_{tenantSlug}_{period}_{yyyyMMdd_HHmmss}.{ext}</c>

/// </summary>

public static class ReportExportFileNames

{

    public const string Prefix = "report";



    public static string Build(

        string? reportType,

        string? tenantSlug,

        string? period,

        string extension = "pdf",

        DateTime? at = null)

    {

        var type = NormalizeReportTypeLabel(reportType);

        return new FileNamingService(NullCurrentTenantAccessor.Instance).GenerateFileName(

            $"{Prefix}_{type}",

            NormalizeExtension(extension),

            additional: period,

            at: at,

            tenantSlug: tenantSlug);

    }



    /// <summary>

    /// Maps internal PDF type keys to download labels (tagesabschluss→tagesbericht, etc.).

    /// </summary>

    public static string NormalizeReportTypeLabel(string? reportType) =>

        reportType?.Trim().ToLowerInvariant() switch

        {

            "tagesabschluss" or "tagesbericht" or "daily" => "tagesbericht",

            "monatsbeleg" or "monatsbericht" or "monthly" => "monatsbericht",

            "jahresbeleg" or "jahresbericht" or "yearly" => "jahresbericht",

            null or "" => "report",

            var other => other.Replace(' ', '_'),

        };



    public static string PeriodFromDay(DateTime date) => date.ToString("yyyyMMdd");



    public static string PeriodFromMonth(DateTime date) => date.ToString("yyyyMM");



    public static string PeriodFromYear(DateTime date) => date.ToString("yyyy");



    public static string PeriodForReportType(string? reportType, DateTime businessDate) =>

        NormalizeReportTypeLabel(reportType) switch

        {

            "monatsbericht" => PeriodFromMonth(businessDate),

            "jahresbericht" => PeriodFromYear(businessDate),

            _ => PeriodFromDay(businessDate),

        };



    private static string NormalizeExtension(string extension)

    {

        var ext = (extension ?? string.Empty).Trim().TrimStart('.');

        return string.IsNullOrEmpty(ext) ? "pdf" : ext.ToLowerInvariant();

    }

}


