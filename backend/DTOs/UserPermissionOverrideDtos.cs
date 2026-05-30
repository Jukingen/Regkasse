namespace KasseAPI_Final.DTOs;

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
    public DateTime? ExpiresAt { get; set; }
}

public sealed class UpsertUserPermissionOverrideRequest
{
    public string Permission { get; set; } = string.Empty;
    public bool IsGranted { get; set; }
    public string? Reason { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public Guid? TenantId { get; set; }
}

public sealed class UserEffectivePermissionsDto
{
    public IReadOnlyList<string> RolePermissions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<UserPermissionOverrideDto> Overrides { get; set; } = Array.Empty<UserPermissionOverrideDto>();
    public IReadOnlyList<string> EffectivePermissions { get; set; } = Array.Empty<string>();
}
