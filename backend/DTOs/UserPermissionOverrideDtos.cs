namespace KasseAPI_Final.DTOs;

public static class UserPermissionOverrideStatuses
{
    public const string Scheduled = "scheduled";
    public const string Active = "active";
    public const string ExpiringSoon = "expiringSoon";
    public const string Expired = "expired";

    public static string Compute(DateTime? validFrom, DateTime? expiresAt, DateTime utcNow, int expiringSoonHours = 48)
    {
        if (expiresAt.HasValue && expiresAt.Value <= utcNow)
            return Expired;
        if (validFrom.HasValue && validFrom.Value > utcNow)
            return Scheduled;
        if (expiresAt.HasValue && expiresAt.Value <= utcNow.AddHours(Math.Max(1, expiringSoonHours)))
            return ExpiringSoon;
        return Active;
    }
}

public sealed class UserPermissionOverrideDto
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public string Permission { get; set; } = string.Empty;
    public bool IsGranted { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ExpiresAt { get; set; }
    /// <summary>scheduled | active | expiringSoon | expired</summary>
    public string Status { get; set; } = UserPermissionOverrideStatuses.Active;
}

public sealed class UpsertUserPermissionOverrideRequest
{
    public string Permission { get; set; } = string.Empty;
    public bool IsGranted { get; set; }
    public string? Reason { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public Guid? TenantId { get; set; }
}

public sealed class UserEffectivePermissionsDto
{
    public IReadOnlyList<string> RolePermissions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<UserPermissionOverrideDto> Overrides { get; set; } = Array.Empty<UserPermissionOverrideDto>();
    public IReadOnlyList<string> EffectivePermissions { get; set; } = Array.Empty<string>();
}
