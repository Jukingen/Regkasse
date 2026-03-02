namespace KasseAPI_Final.Services;

/// <summary>
/// QR kod görsel üretimi - payment.tse.qrPayload'tan PNG/SVG.
/// </summary>
public interface IQrImageService
{
    /// <summary>
    /// PNG formatında QR image döner. 256x256 print odaklı.
    /// </summary>
    Task<byte[]?> GetQrPngAsync(Guid paymentId, CancellationToken ct = default);

    /// <summary>
    /// SVG formatında QR image döner. Vektörel, baskı için uygun.
    /// </summary>
    Task<string?> GetQrSvgAsync(Guid paymentId, CancellationToken ct = default);
}
