using System.ComponentModel.DataAnnotations;
using KasseAPI_Final.Models.DTOs;

namespace KasseAPI_Final.DTOs;

/// <summary>List filter for admin session management (<c>GET /api/admin/sessions</c>).</summary>
public sealed class AdminSessionListFilter
{
    public const string StatusActive = "active";
    public const string StatusExpired = "expired";
    public const string StatusAll = "all";

    /// <summary>User name, email, display name, user id, IP, or device (case-insensitive).</summary>
    public string? Search { get; init; }

    /// <summary>Super Admin only. Managers are always scoped to their ambient tenant.</summary>
    public Guid? TenantId { get; init; }

    public string? Role { get; init; }

    /// <summary><c>active</c> (default), <c>expired</c>, or <c>all</c>.</summary>
    public string? Status { get; init; }

    public string? UserId { get; init; }
}

/// <summary>Caller scope for admin session list / logout (tenant isolation + current session).</summary>
public sealed class SessionManagementAccess
{
    public required string ActorUserId { get; init; }
    public required string ActorRole { get; init; }
    public bool IsSuperAdmin { get; init; }
    public Guid? CurrentSessionId { get; init; }

    /// <summary>When set, reads and logouts are limited to this tenant (Mandanten-Admin).</summary>
    public Guid? ScopedTenantId { get; init; }
}

/// <summary>Auth session row for Super Admin (all tenants) and Manager (own tenant).</summary>
public sealed class AdminActiveSessionDto
{
    public Guid Id { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string? UserName { get; init; }
    public string? Email { get; init; }
    public string? DisplayName { get; init; }
    public string? Role { get; init; }
    public string ClientApp { get; init; } = string.Empty;
    public string? DeviceId { get; init; }
    public string? DeviceName { get; init; }
    public string? Browser { get; init; }
    public string? OS { get; init; }

    /// <summary>Human-readable platform, e.g. <c>POS (Android)</c>, <c>POS (iOS)</c>, <c>POS (Web)</c>, <c>Admin</c>.</summary>
    public string? PlatformLabel { get; init; }

    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public Guid? TenantId { get; init; }
    public string? TenantName { get; init; }
    public DateTime StartedAtUtc { get; init; }
    public DateTime LastActivityAtUtc { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
    public int DurationSeconds { get; init; }
    public bool IsActive { get; init; }
    public bool IsCurrent { get; init; }
}

public sealed class LogoutBulkSessionsRequestDto
{
    [Required]
    [MinLength(1)]
    [MaxLength(200)]
    public List<Guid> SessionIds { get; set; } = [];
}

public readonly record struct AdminSessionTerminateResult(bool Success, bool IsCurrentSession)
{
    public static AdminSessionTerminateResult Ok() => new(true, false);
    public static AdminSessionTerminateResult NotFound() => new(false, false);
    public static AdminSessionTerminateResult CurrentSession() => new(false, true);
}

public sealed class TerminateSessionResultDto
{
    public bool Success { get; init; }
}

public sealed class TerminateSessionsCountDto
{
    public int TerminatedCount { get; init; }
}

public sealed class ForceLogoutResultDto
{
    public bool Success { get; init; }
}

/// <summary>Maps <see cref="ActiveSessionDto"/> device fields plus user identity for the admin list.</summary>
public static class AdminActiveSessionDtoMapper
{
    public static AdminActiveSessionDto From(
        ActiveSessionDto session,
        string? userName,
        string? email,
        string? displayName,
        string? role,
        Guid? tenantId,
        string? tenantName = null,
        string? platformLabel = null,
        int durationSeconds = 0)
    {
        return new AdminActiveSessionDto
        {
            Id = session.Id,
            UserId = session.UserId,
            UserName = userName,
            Email = email,
            DisplayName = displayName,
            Role = role,
            ClientApp = session.ClientApp,
            DeviceId = session.DeviceId,
            DeviceName = session.DeviceName,
            Browser = session.Browser,
            OS = session.OS,
            IpAddress = session.IpAddress,
            UserAgent = session.UserAgent,
            TenantId = tenantId,
            TenantName = tenantName,
            StartedAtUtc = session.StartedAtUtc,
            LastActivityAtUtc = session.LastActivityAtUtc,
            ExpiresAtUtc = session.ExpiresAtUtc,
            DurationSeconds = durationSeconds,
            PlatformLabel = platformLabel,
            IsActive = session.IsActive,
            IsCurrent = session.IsCurrent,
        };
    }
}
