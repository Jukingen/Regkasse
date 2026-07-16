namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Result of TSE / RKSV compact-JWS signature verification (real crypto or development simulation).
/// </summary>
public sealed class TseVerificationResult
{
    public bool IsValid { get; init; }

    public bool IsSimulated { get; init; }

    public string Message { get; init; } = string.Empty;

    /// <summary>Admin signature-debug code: PASS | FAIL | SIMULATED.</summary>
    public string ToVerifyResultCode() =>
        IsSimulated
            ? "SIMULATED"
            : IsValid
                ? "PASS"
                : "FAIL";
}
