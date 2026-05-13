namespace KasseAPI_Final.Models;

/// <summary>Public POS contract for <c>GET /api/license/status</c>.</summary>
public sealed class LicensePublicStatusDto
{
    /// <summary><c>Trial</c> or <c>Paid</c> (capitalized for stable client enums).</summary>
    public string LicenseType { get; init; } = "Trial";

    /// <summary>UTC instant when the current mode ends (trial end or JWT exp), ISO-8601.</summary>
    public string? ValidUntil { get; init; }

    public int DaysRemaining { get; init; }

    public IReadOnlyList<string> Features { get; init; } = Array.Empty<string>();

    public bool IsExpired { get; init; }

    public bool IsValid { get; init; }
}

/// <summary>Optional POS feature flags from <c>GET /api/license/features</c>.</summary>
public sealed class LicenseFeaturesDto
{
    public bool AllowOffline { get; init; }

    /// <summary>Maximum concurrent cashiers; <c>-1</c> means unlimited.</summary>
    public int MaxCashiers { get; init; }
}

/// <summary>Result of async license validation (middleware + diagnostics).</summary>
public sealed class LicenseValidationResult
{
    public bool IsLicenseOperational { get; init; }

    public bool IsTrial { get; init; }

    public bool IsExpired { get; init; }

    public bool IsPaidValid { get; init; }

    public int DaysRemaining { get; init; }

    public DateTime? ExpiryUtc { get; init; }
}
