namespace KasseAPI_Final.Services.License;

/// <summary>Canonical license lookup / activation error codes (API + FA).</summary>
public static class LicenseKeyErrorCodes
{
    public const string InvalidFormat = "INVALID_LICENSE_FORMAT";
    public const string SystemLicenseExpected = "SYSTEM_LICENSE_EXPECTED";
    public const string TenantLicenseExpected = "TENANT_LICENSE_EXPECTED";
    public const string NotFound = "LICENSE_NOT_FOUND";
    public const string AlreadyActivated = "LICENSE_ALREADY_ACTIVATED";
    public const string Expired = "expired";
    public const string SlugMismatch = "slug_mismatch";
    public const string Revoked = "revoked";

    /// <summary>Pre-unification lowercase codes still accepted by FA.</summary>
    public const string InvalidFormatLegacy = "invalid_format";

    public const string NotFoundLegacy = "not_found";

    public static bool IsInvalidFormat(string? code) =>
        EqualsOrdinal(code, InvalidFormat) || EqualsOrdinal(code, InvalidFormatLegacy);

    public static bool IsNotFound(string? code) =>
        EqualsOrdinal(code, NotFound) || EqualsOrdinal(code, NotFoundLegacy);

    private static bool EqualsOrdinal(string? a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
