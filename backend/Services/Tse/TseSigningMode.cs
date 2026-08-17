namespace KasseAPI_Final.Services.Tse;

/// <summary>Resolved signer for a fiscal receipt (POS payment / special receipt / invoice).</summary>
public enum TseSigningMode
{
    /// <summary>No allowed signer. Callers must fail closed.</summary>
    Disabled = 0,

    /// <summary>fiskaly SIGN AT receipt-level signing (<c>SignReceiptAsync</c>).</summary>
    Fiskaly = 1,

    /// <summary>Development-only Soft TSE (simulated compact JWS).</summary>
    SoftTse = 2,

    /// <summary>Local <see cref="Tse.SignaturePipeline"/> with <c>SoftwareTseKeyProvider</c> (Development).</summary>
    LocalPipeline = 3
}
