namespace KasseAPI_Final.Configuration;

/// <summary>
/// Per-vendor connection settings under <c>Tse:Providers:{name}</c>
/// (fiskaly / epson / swissbit). Secrets should come from env / user-secrets in real deployments.
/// </summary>
public sealed class TseVendorConnectionOptions
{
    /// <summary>When false, the vendor is ignored even if keys are present.</summary>
    public bool Enabled { get; set; } = true;

    public string ApiBaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>Optional SCU / TSE serial (fiskaly Signature Creation Unit id).</summary>
    public string? SignatureCreationUnitId { get; set; }

    /// <summary>Optional leaf cert DER base64 (fiskaly DEP / verification).</summary>
    public string? SigningCertificateDerBase64 { get; set; }

    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ApiSecret);

    public bool IsUsable => Enabled && HasCredentials;
}
