namespace KasseAPI_Final.Services.Billing;

public interface IBillingService
{
    // Preview license before sale
    Task<LicenseSalePreviewResponse> PreviewLicenseSaleAsync(
        LicenseSalePreviewRequest request,
        CancellationToken ct = default);

    // Create a new license sale
    Task<LicenseSaleResponse> CreateLicenseSaleAsync(
        CreateLicenseSaleRequest request,
        Guid soldByUserId,
        CancellationToken ct = default);

    // Get a single sale
    Task<LicenseSaleResponse> GetLicenseSaleAsync(
        Guid saleId,
        CancellationToken ct = default);

    // List sales with filters
    Task<LicenseSaleListResponse> ListLicenseSalesAsync(
        LicenseSaleListQuery query,
        CancellationToken ct = default);

    // Cancel a sale
    Task<LicenseSaleResponse> CancelLicenseSaleAsync(
        Guid saleId,
        CancelLicenseSaleRequest request,
        Guid cancelledByUserId,
        CancellationToken ct = default);

    // Get stats
    Task<LicenseSaleStatsResponse> GetLicenseSaleStatsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken ct = default);

    // Get sale by license key
    Task<LicenseSaleResponse?> GetSaleByLicenseKeyAsync(
        string licenseKey,
        CancellationToken ct = default);

    // Check if license key is valid and available
    Task<bool> IsLicenseKeyValidAsync(
        string licenseKey,
        CancellationToken ct = default);

    // Get next invoice number
    Task<string> GetNextInvoiceNumberAsync(
        DateTime date,
        CancellationToken ct = default);

    // Generate and persist license sale invoice PDF
    Task<byte[]> GenerateInvoicePdfAsync(
        Guid saleId,
        CancellationToken ct = default);
}
