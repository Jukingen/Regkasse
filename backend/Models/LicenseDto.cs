namespace KasseAPI_Final.Models;

/// <summary>Public POS/FA contract for <c>GET /api/license/status</c> (also referred to as license status read model).</summary>
public sealed class LicensePublicStatusDto
{
    /// <summary><c>Trial</c>, <c>Licensed</c>, <c>Expired</c>, or <c>Demo</c> (development-only snapshot).</summary>
    public string LicenseType { get; init; } = "Trial";

    /// <summary>UTC instant when the current mode ends (trial end or JWT exp). Serialized as ISO-8601.</summary>
    public DateTime? ValidUntil { get; init; }

    public int DaysRemaining { get; init; }

    public IReadOnlyList<string> Features { get; init; } = Array.Empty<string>();

    public bool IsExpired { get; init; }

    public bool IsValid { get; init; }

    /// <summary>Coarse deployment display: <c>Demo</c> (local dev bypass), <c>Trial</c>, or <c>Production</c>.</summary>
    public string Mode { get; init; } = "Production";
}

/// <summary>Optional POS feature flags from <c>GET /api/license/features</c> plus enabled license feature ids.</summary>
public sealed class LicenseFeaturesDto
{
    public bool AllowOffline { get; init; }

    /// <summary>Maximum concurrent cashiers; <c>-1</c> means unlimited.</summary>
    public int MaxCashiers { get; init; }

    /// <summary>Enabled <see cref="LicenseFeatureIds"/> for this deployment (trial = full bundle).</summary>
    public IReadOnlyList<string> EnabledLicenseFeatures { get; init; } = Array.Empty<string>();
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
