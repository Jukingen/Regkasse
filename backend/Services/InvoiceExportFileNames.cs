using KasseAPI_Final.Tenancy;



namespace KasseAPI_Final.Services;



/// <summary>

/// Canonical invoice download / attachment file names (via <see cref="IFileNamingService"/>).

/// PDF: <c>invoice_{tenantSlug}[_{registerNumber}]_{invoiceNumber}_{yyyyMMdd_HHmmss}.pdf</c>

/// CSV/Excel list: <c>invoices_{tenantSlug}_{fromDate}_{toDate}_{yyyyMMdd_HHmmss}.{ext}</c>

/// </summary>

public static class InvoiceExportFileNames

{

    public const string PdfPrefix = "invoice";

    public const string ListPrefix = "invoices";



    /// <summary>Test/helper mirror of <see cref="IFileNamingService.GenerateFileName"/> for invoice PDFs.</summary>

    public static string BuildPdf(

        string? tenantSlug,

        string? registerNumber,

        string? invoiceNumber,

        DateTime? at = null) =>

        new FileNamingService(NullCurrentTenantAccessor.Instance).GenerateFileName(

            PdfPrefix,

            "pdf",

            registerId: registerNumber,

            additional: string.IsNullOrWhiteSpace(invoiceNumber) ? "invoice" : invoiceNumber,

            at: at,

            tenantSlug: tenantSlug);



    public static string BuildList(

        string? tenantSlug,

        DateTime? fromDate,

        DateTime? toDate,

        string extension = "csv",

        DateTime? at = null) =>

        new FileNamingService(NullCurrentTenantAccessor.Instance).GenerateFileName(

            ListPrefix,

            extension,

            registerId: ExportFileNameSegments.DateOnly(fromDate),

            additional: ExportFileNameSegments.DateOnly(toDate),

            at: at,

            tenantSlug: tenantSlug);

}


