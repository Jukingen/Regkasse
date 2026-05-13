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

    /// <summary>
    /// RKSV receipt reprint: encodes the exact stored QR string (e.g. <see cref="Models.Receipt.QrCodePayload"/>) to PNG
    /// without alternate compression layers so the scanned payload matches the persisted receipt.
    /// </summary>
    byte[]? GetQrPngFromExactPayload(string? payload);

    /// <summary>
    /// Best-effort QR PNG (auto QR version, ECC M then ECC L with smaller modules). Returns null if encoding fails.
    /// <see cref="GetQrPngFromExactPayload"/> invokes this after the strict version sweep (and after UTF-8 truncation retry) as a last resort.
    /// </summary>
    byte[]? GenerateQrCodeImage(string? payload);
}
