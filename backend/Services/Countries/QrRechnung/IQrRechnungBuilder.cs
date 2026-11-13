namespace KasseAPI_Final.Services.Countries.QrRechnung;

/// <summary>
/// Swiss QR-Rechnung (SIX QR-bill) builder. Payload is shape-only; PDF/QR image is not implemented.
/// </summary>
public interface IQrRechnungBuilder
{
    Task<QrRechnungPayload> BuildPayloadAsync(
        QrRechnungRequest request,
        CancellationToken cancellationToken = default);

    Task<byte[]> BuildPdfAsync(
        QrRechnungRequest request,
        CancellationToken cancellationToken = default);
}
