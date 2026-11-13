namespace KasseAPI_Final.Services;

/// <summary>
/// Read-only PDF generation for receipt reprints (no new signing, no new DB rows).
/// </summary>
public interface IReceiptPdfService
{
    /// <summary>
    /// Builds a PDF copy of the persisted receipt for <paramref name="paymentId"/> (normal or RKSV special).
    /// </summary>
    Task<byte[]> GeneratePdfAsync(
        Guid paymentId,
        bool includeReprintWatermark = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a watermarked PDF copy of the persisted receipt for <paramref name="paymentId"/> (normal or RKSV special).
    /// </summary>
    Task<byte[]> GenerateReprintPdfAsync(Guid paymentId, CancellationToken cancellationToken = default);
}
