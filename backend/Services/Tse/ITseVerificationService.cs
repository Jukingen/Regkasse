namespace KasseAPI_Final.Services.Tse;

public interface ITseVerificationService
{
    /// <summary>
    /// Verifies a compact RKSV JWS. In TSE simulation / demo, non-cryptographic FakeTseProvider
    /// signatures are accepted as simulated (not fiscally valid).
    /// </summary>
    Task<TseVerificationResult> VerifySignatureAsync(
        string signature,
        CancellationToken cancellationToken = default);
}
