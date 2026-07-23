namespace KasseAPI_Final.Configuration;

/// <summary>
/// Platform-wide session concurrency limits. Bound from <c>SessionPolicy</c> in appsettings.
/// Tenant idle-timeout overrides (warning / keep-cart) remain in <c>SystemSettings</c>.
/// </summary>
public sealed class SessionPolicyOptions
{
    public const string SectionName = "SessionPolicy";

    /// <summary>Maximum concurrent active sessions per user. Default 1.</summary>
    public int MaxConcurrentSessions { get; set; } = 1;

    /// <summary>Idle / inactivity timeout in minutes when no tenant override exists. Default 30.</summary>
    public int SessionTimeoutMinutes { get; set; } = 30;

    /// <summary>When false, additional devices should be rejected once the concurrent limit is reached.</summary>
    public bool AllowMultipleDevices { get; set; } = false;
}
