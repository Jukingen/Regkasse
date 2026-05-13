namespace KasseAPI_Final.Configuration;

/// <summary>
/// Optional PEM file paths and JWT issuer/audience for offline license tokens.
/// Merged into <see cref="LicenseOptions"/> at startup when inline PEM fields are empty.
/// </summary>
public sealed class LicenseSettingsOptions
{
    public const string SectionName = "LicenseSettings";

    public string? Issuer { get; set; }

    public string? Audience { get; set; }

    /// <summary>Filesystem path to RSA public key PEM (verification).</summary>
    public string? PublicKeyPath { get; set; }

    /// <summary>Filesystem path to RSA private key PEM (issuance only).</summary>
    public string? PrivateKeyPath { get; set; }
}
