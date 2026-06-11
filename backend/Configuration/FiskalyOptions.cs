namespace KasseAPI_Final.Configuration;

/// <summary>
/// fiskaly SIGN AT (cloud SCU / hardware TSE) integration settings.
/// Private keys remain on fiskaly; signing is delegated via <see cref="Tse.Fiskaly.IFiskalyClient"/>.
/// </summary>
public sealed class FiskalyOptions
{
    public const string SectionName = "Fiskaly";

    /// <summary>When true and <c>Tse:TseMode=Device</c>, registers <see cref="Tse.FiskalyTseKeyProvider"/>.</summary>
    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = "https://rksv.fiskaly.com/api/v1";

    /// <summary>Alias for <see cref="BaseUrl"/> (<c>appsettings.Production.json</c>).</summary>
    public string ApiBaseUrl
    {
        get => BaseUrl;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
                BaseUrl = value;
        }
    }

    public string ApiKey { get; set; } = string.Empty;

    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>fiskaly Signature Creation Unit (SCU) UUID — maps to TSE serial in RKSV context.</summary>
    public string SignatureCreationUnitId { get; set; } = string.Empty;

    /// <summary>Alias for <see cref="SignatureCreationUnitId"/> (<c>appsettings.Production.json</c>).</summary>
    public string TseSerialNumber
    {
        get => SignatureCreationUnitId;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
                SignatureCreationUnitId = value;
        }
    }

    /// <summary>
    /// Leaf signing certificate (DER, Base64). Required for DEP export / verification because
    /// SIGN AT does not expose the full X.509 via the SCU retrieve endpoint.
    /// </summary>
    public string? SigningCertificateDerBase64 { get; set; }

    /// <summary>Optional issuer CA certificates (DER, Base64 each) for DEP <c>Zertifizierungsstellen</c>.</summary>
    public List<string> IssuerCertificatesDerBase64 { get; set; } = new();

    /// <summary>FinanzOnline-registered AES-256 turnover counter key (32 bytes, Base64).</summary>
    public string? TurnoverCounterAesKeyBase64 { get; set; }

    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(ApiSecret)
        && !string.IsNullOrWhiteSpace(SignatureCreationUnitId)
        && !string.IsNullOrWhiteSpace(SigningCertificateDerBase64);
}
