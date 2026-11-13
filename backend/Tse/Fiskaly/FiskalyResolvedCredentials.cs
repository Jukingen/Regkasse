namespace KasseAPI_Final.Tse.Fiskaly;

public static class FiskalyCredentialSources
{
    public const string Tenant = "tenant";
    public const string Host = "host";
}

/// <summary>
/// Plaintext credentials for one call. <see cref="TenantId"/> is null for the Development host fallback.
/// </summary>
public sealed record FiskalyResolvedCredentials(
    Guid? TenantId,
    string ApiKey,
    string ApiSecret,
    string ScuId,
    string Environment,
    string? SigningCertificateDerBase64,
    IReadOnlyList<string> IssuerCertificatesDerBase64,
    string Source);
