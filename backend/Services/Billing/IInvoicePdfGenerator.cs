namespace KasseAPI_Final.Services.Billing;

public interface IInvoicePdfGenerator
{
    // Generate PDF for a license sale
    Task<byte[]> GenerateInvoicePdfAsync(
        Guid saleId,
        CancellationToken ct = default);

    // Generate preview PDF (without saving)
    Task<byte[]> GeneratePreviewPdfAsync(
        LicenseSalePreviewResponse preview,
        CancellationToken ct = default);

    // Get PDF as base64 for inline display
    Task<string> GetInvoicePdfBase64Async(
        Guid saleId,
        CancellationToken ct = default);
}
