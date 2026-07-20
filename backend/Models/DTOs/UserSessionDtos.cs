namespace KasseAPI_Final.Models.DTOs;

public sealed class ActiveSessionDto
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string ClientApp { get; set; } = string.Empty;
    public string? DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public string? Browser { get; set; }
    public string? OS { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime LastActivityAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public bool IsActive { get; set; }
    public bool IsCurrent { get; set; }
}

/// <summary>
/// Sketch-aligned device session DTO (no access-token field).
/// Mapped from <see cref="ActiveSessionDto"/> / <c>auth_sessions</c>.
/// </summary>
public sealed class UserSessionDto
{
    public Guid Id { get; set; }
    public string? DeviceName { get; set; }
    public string? Browser { get; set; }
    public string? OS { get; set; }
    public string? IPAddress { get; set; }
    public DateTime LastActiveAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsCurrent { get; set; }
    public bool IsActive { get; set; }
    public string ClientApp { get; set; } = string.Empty;
}

public sealed class TenantSessionPolicyDto
{
    public int SessionTimeoutMinutes { get; set; } = 30;
    public int WarningBeforeTimeoutMinutes { get; set; } = 5;
    public bool KeepCartAfterTimeout { get; set; } = true;
    public bool IdleTimeoutEnabled { get; set; } = true;
}
