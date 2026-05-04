namespace KasseAPI_Final.Rksv;

/// <summary>
/// Parsed RKSV machine-readable QR payload. No cryptographic verification.
/// </summary>
public sealed class RksvQrPayload
{
    /// <summary>RKSV algorithm id, e.g. <c>R1-AT1</c>.</summary>
    public string AlgorithmId { get; init; } = string.Empty;

    public RksvQrPayloadLayout Layout { get; init; }

    /// <summary>Kassenidentifikationsnummer (register id segment).</summary>
    public string CashRegisterId { get; init; } = string.Empty;

    public string ReceiptNumber { get; init; } = string.Empty;

    /// <summary>Timestamp / Sollzeit segment as printed (not normalized).</summary>
    public string Timestamp { get; init; } = string.Empty;

    /// <summary>
    /// Gross amounts per tax bucket. For <see cref="RksvQrPayloadLayout.StandardRksvV1"/> uses RKSV bucket codes;
    /// for <see cref="RksvQrPayloadLayout.InternalCompact"/> uses two synthetic slots.
    /// </summary>
    public IReadOnlyList<RksvQrTaxBucket> TaxBuckets { get; init; } = Array.Empty<RksvQrTaxBucket>();

    /// <summary>Verschlüsselter Umsatzzähler; absent in <see cref="RksvQrPayloadLayout.InternalCompact"/>.</summary>
    public string? EncryptedTurnoverCounter { get; init; }

    public string CertificateSerial { get; init; } = string.Empty;

    /// <summary>Previous receipt signature segment when present on the wire.</summary>
    public string? PreviousSignature { get; init; }

    /// <summary>Trailing compact JWS (header.payload.signature).</summary>
    public string Signature { get; init; } = string.Empty;
}
