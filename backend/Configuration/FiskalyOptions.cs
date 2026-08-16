namespace KasseAPI_Final.Configuration;

/// <summary>
/// fiskaly SIGN AT (cloud SCU / hardware TSE) integration settings.
/// Private keys remain on fiskaly; signing is delegated via <see cref="Tse.Fiskaly.IFiskalyClient"/>.
/// </summary>
public sealed class FiskalyOptions
{
    public const string SectionName = "Fiskaly";
    public const string LiveEnvironment = "LIVE";
    public const string TestEnvironment = "TEST";

    /// <summary>
    /// Config default for the SIGN AT client (on unless overridden). Mandanten-Admin / Super Admin
    /// may overlay this at runtime via <c>tenant_settings</c> key <c>Fiskaly:Enabled</c>
    /// (see <see cref="Tse.Fiskaly.FiskalyEnabledOverrideCache"/>). Tenant overlay wins over global.
    /// </summary>
    public bool Enabled { get; set; } = true;

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

    /// <summary>Alias for <see cref="SignatureCreationUnitId"/> (operator-facing SIGN AT id).</summary>
    public string ScuId
    {
        get => SignatureCreationUnitId;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
                SignatureCreationUnitId = value;
        }
    }

    /// <summary>
    /// Deployment label: <c>TEST</c> or <c>LIVE</c>. When empty, inferred from <see cref="BaseUrl"/>.
    /// </summary>
    public string Environment { get; set; } = TestEnvironment;

    /// <summary>
    /// Leaf signing certificate (DER, Base64). Required for DEP export / verification because
    /// SIGN AT does not expose the full X.509 via the SCU retrieve endpoint.
    /// </summary>
    public string? SigningCertificateDerBase64 { get; set; }

    /// <summary>Optional issuer CA certificates (DER, Base64 each) for DEP <c>Zertifizierungsstellen</c>.</summary>
    public List<string> IssuerCertificatesDerBase64 { get; set; } = new();

    /// <summary>FinanzOnline-registered AES-256 turnover counter key (32 bytes, Base64).</summary>
    public string? TurnoverCounterAesKeyBase64 { get; set; }

    /// <summary>
    /// Bearer cache lifetime in minutes when fiskaly omits <c>expires_at</c>.
    /// Default 1380 (23 hours) so the token is refreshed before a typical 24h expiry.
    /// </summary>
    public int TokenCacheMinutes { get; set; } = 23 * 60;

    /// <summary>Alias for <see cref="TokenCacheMinutes"/> (hours, rounded up on get).</summary>
    public int TokenCacheHours
    {
        get => Math.Max(1, (ResolveTokenCacheMinutes() + 59) / 60);
        set
        {
            if (value > 0)
                TokenCacheMinutes = value * 60;
        }
    }

    /// <summary>HTTP retries for transient fiskaly failures (auth, 429, 5xx).</summary>
    public int MaxRetries { get; set; } = 3;

    public int RetryDelaySeconds { get; set; } = 2;

    /// <summary>API key + secret are present (does not require <see cref="Enabled"/>).</summary>
    public bool HasApiCredentials =>
        !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(ApiSecret);

    /// <summary>Config-only: <see cref="Enabled"/> and API credentials. Runtime overlay is applied separately.</summary>
    public bool HasCredentials => Enabled && HasApiCredentials;

    public bool IsConfigured =>
        HasCredentials
        && !string.IsNullOrWhiteSpace(SignatureCreationUnitId)
        && !string.IsNullOrWhiteSpace(SigningCertificateDerBase64);

    public bool IsEffectivelyEnabled(bool? runtimeOverride) => runtimeOverride ?? Enabled;

    public bool HasActiveCredentials(bool? runtimeOverride) =>
        IsEffectivelyEnabled(runtimeOverride) && HasApiCredentials;

    public string ResolveEnvironment()
    {
        var explicitEnv = Environment?.Trim();
        if (!string.IsNullOrWhiteSpace(explicitEnv))
        {
            if (explicitEnv.Equals(LiveEnvironment, StringComparison.OrdinalIgnoreCase)
                || explicitEnv.Equals("PROD", StringComparison.OrdinalIgnoreCase)
                || explicitEnv.Equals("PRODUCTION", StringComparison.OrdinalIgnoreCase))
            {
                return LiveEnvironment;
            }

            return TestEnvironment;
        }

        var url = BaseUrl ?? string.Empty;
        if (url.Contains("api.fiskaly.com", StringComparison.OrdinalIgnoreCase)
            && !url.Contains("rksv.fiskaly.com", StringComparison.OrdinalIgnoreCase))
        {
            return LiveEnvironment;
        }

        return TestEnvironment;
    }

    public int ResolveTokenCacheMinutes() =>
        TokenCacheMinutes > 0 ? TokenCacheMinutes : 23 * 60;

    public TimeSpan ResolveTokenCacheLifetime() =>
        TimeSpan.FromMinutes(ResolveTokenCacheMinutes());

    public int ResolveMaxRetries() => Math.Clamp(MaxRetries, 1, 8);

    public TimeSpan ResolveRetryDelay() =>
        TimeSpan.FromSeconds(Math.Clamp(RetryDelaySeconds, 1, 30));
}
