namespace KasseAPI_Final.Rksv;

/// <summary>One tax-rate gross bucket from the QR (raw amount string, no arithmetic).</summary>
public sealed record RksvQrTaxBucket(string Code, string Amount);
