namespace KasseAPI_Final.Models.Export;

/// <summary>Compact JWS row for DEP export grouping (payment, special receipt, or daily closing).</summary>
public sealed class RksvDepReceiptSignatureInfo
{
    public string TseSignature { get; set; } = string.Empty;

    public DateTime IssuedAt { get; set; }

    public string CertificateThumbprint { get; set; } = string.Empty;

    /// <summary><c>Normal</c>, <c>DailyClosing</c>, or an <see cref="Models.RksvSpecialReceiptKinds"/> value.</summary>
    public string? ReceiptType { get; set; }

    /// <summary>RKSV BelegNr sequence when parseable; otherwise a type-specific fallback for ordering.</summary>
    public int SequenceNumber { get; set; }
}
