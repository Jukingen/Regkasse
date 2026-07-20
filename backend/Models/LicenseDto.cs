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

    /// <summary>True when <see cref="IDevelopmentModeService.ShouldBypassLicense"/> supplied the current snapshot (Development host only).</summary>
    public bool IsDevelopmentBypass { get; init; }

    /// <summary>Mandant access flag when <c>tenantId</c> query or tenant context is resolved; otherwise null.</summary>
    public bool? CanAccess { get; init; }

    /// <summary>Mandant transaction flag when tenant context is resolved; otherwise null.</summary>
    public bool? CanTransact { get; init; }

    /// <summary>German mandant status copy when tenant context is resolved (localized when Accept-Language is set).</summary>
    public string? StatusMessage { get; init; }

    /// <summary>Stable message key for clients (<c>license.status.*</c>).</summary>
    public string? StatusMessageKey { get; init; }

    /// <summary>True when mandant license is expired but still within the grace window.</summary>
    public bool IsInGracePeriod { get; init; }

    /// <summary>True when mandant license is past grace (POS locked).</summary>
    public bool IsLocked { get; init; }

    /// <summary>Elapsed whole days since expiry when expired; otherwise 0.</summary>
    public int DaysOverdue { get; init; }

    /// <summary>Remaining mandant grace days when <see cref="IsInGracePeriod"/> is true.</summary>
    public int GracePeriodRemaining { get; init; }

    /// <summary>UTC instant when POS lock starts (expiry + grace); null when not expired.</summary>
    public DateTime? LockDate { get; init; }

    /// <summary>Restriction codes (e.g. <c>POS_LOCKED</c>, <c>SUPERADMIN_UNLOCK_ONLY</c>).</summary>
    public IReadOnlyList<string> Restrictions { get; init; } = Array.Empty<string>();

    /// <summary>True when mandant license requires renewal (lockdown).</summary>
    public bool RequiresRenewal { get; init; }
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
