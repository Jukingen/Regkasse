using KasseAPI_Final.Models.DTOs;

namespace KasseAPI_Final.DTOs;

/// <summary>Active auth session for Super Admin session management (all users).</summary>
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
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public Guid? TenantId { get; init; }
    public DateTime StartedAtUtc { get; init; }
    public DateTime LastActivityAtUtc { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
    public bool IsActive { get; init; }
    public bool IsCurrent { get; init; }
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
        Guid? tenantId)
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
            StartedAtUtc = session.StartedAtUtc,
            LastActivityAtUtc = session.LastActivityAtUtc,
            ExpiresAtUtc = session.ExpiresAtUtc,
            IsActive = session.IsActive,
            IsCurrent = session.IsCurrent,
        };
    }
}
