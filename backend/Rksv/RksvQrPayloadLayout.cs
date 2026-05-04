namespace KasseAPI_Final.Rksv;

/// <summary>Detected wire layout after algorithm prefix.</summary>
public enum RksvQrPayloadLayout
{
    /// <summary>
    /// Regkasse compact QR: <c>kasse_beleg_ts_amount1_amount2_cert_JWS</c> (6 body segments + signature).
    /// </summary>
    InternalCompact = 0,

    /// <summary>
    /// Classic RKSV-style line: five tax gross buckets, encrypted turnover, cert, previous sig, then JWS.
    /// </summary>
    StandardRksvV1 = 1
}
