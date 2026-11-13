namespace KasseAPI_Final.Models.DTOs;

public sealed class ActiveSessionDto
{
    public Guid Id { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string ClientApp { get; init; } = string.Empty;
    public string? DeviceId { get; init; }
    public string? DeviceName { get; init; }
    public string? Browser { get; init; }
    public string? OS { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public DateTime StartedAtUtc { get; init; }
    public DateTime LastActivityAtUtc { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
    public bool IsActive { get; init; }
    public bool IsCurrent { get; init; }
}

/// <summary>
/// Device-session list shape for <c>/api/user/sessions/devices</c>
/// (mapped from <see cref="ActiveSessionDto"/>).
/// </summary>
public sealed class UserSessionDto
{
    public Guid Id { get; init; }
    public string? DeviceName { get; init; }
    public string? Browser { get; init; }
    public string? OS { get; init; }
    public string? IPAddress { get; init; }
    public DateTime LastActiveAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public bool IsCurrent { get; init; }
    public bool IsActive { get; init; }
    public string ClientApp { get; init; } = string.Empty;
}

/// <summary>
/// Effective session policy for <c>GET /api/user/session-policy</c> and Auth <c>/me</c>.
/// Concurrent-session limits come from <c>SessionPolicy</c> configuration;
/// idle-timeout fields may be overridden per tenant via <c>SystemSettings</c>.
/// </summary>
public sealed class TenantSessionPolicyDto
{
    /// <summary>Maximum concurrent active sessions per user (from <c>SessionPolicy:MaxConcurrentSessions</c>).</summary>
    public int MaxConcurrentSessions { get; init; } = 1;

    /// <summary>Idle / inactivity timeout in minutes.</summary>
    public int SessionTimeoutMinutes { get; init; } = 30;

    /// <summary>When false, additional devices should be rejected once the concurrent limit is reached.</summary>
    public bool AllowMultipleDevices { get; init; } = false;

    public int WarningBeforeTimeoutMinutes { get; init; } = 5;
    public bool KeepCartAfterTimeout { get; init; } = true;
    public bool IdleTimeoutEnabled { get; init; } = true;
}
