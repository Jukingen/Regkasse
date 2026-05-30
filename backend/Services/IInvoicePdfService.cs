namespace KasseAPI_Final.Services;

/// <summary>
/// Invoice PDF generation and email resend (uses persisted invoice / payment snapshot only).
/// </summary>
public interface IInvoicePdfService
{
    Task<byte[]> GenerateInvoicePdfAsync(Guid invoiceId, bool copy = false, CancellationToken cancellationToken = default);

    Task<Stream> GetInvoicePdfStreamAsync(Guid invoiceId, bool copy = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Regenerates the invoice PDF and emails it to the recipient. Returns false when invoice or recipient is missing.
    /// </summary>
    Task<bool> ResendInvoiceEmailAsync(
        Guid invoiceId,
        string? recipientEmail,
        CancellationToken cancellationToken = default);
}
