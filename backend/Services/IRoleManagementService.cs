using KasseAPI_Final.Authorization;

namespace KasseAPI_Final.Services;

/// <summary>
/// Use-case layer for role and permission management: catalog, list roles with permissions, set custom role permissions, delete custom roles.
/// Business rules: SuperAdmin role is read-only for permissions (matrix-only). Other canonical roles
/// may be updated via claims by SuperAdmin. Only custom roles can be deleted; canonical roles cannot
/// be deleted; delete blocked when role has assigned users.
/// </summary>
public interface IRoleManagementService
{
    /// <summary>
    /// Returns the permission catalog (key, group, resource, action, description).
    /// </summary>
    IReadOnlyList<PermissionCatalogMetadata.Item> GetPermissionsCatalog();

    /// <summary>
    /// Returns all roles with their effective permissions, isSystemRole flag, and user count.
    /// </summary>
    Task<IReadOnlyList<RoleWithPermissionsDto>> GetRolesWithPermissionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets permissions via AspNetRoleClaims. SuperAdmin role returns error (matrix-only). Other
    /// canonical roles accept updates (claims override matrix when present).
    /// Invalid permission keys return validation error. Empty list is allowed.
    /// </summary>
    Task<SetRolePermissionsResult> SetRolePermissionsAsync(string roleName, IReadOnlyList<string> permissionKeys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a custom role. Fails if system role or if any user is assigned to the role.
    /// </summary>
    Task<DeleteRoleResult> DeleteRoleAsync(string roleName, CancellationToken cancellationToken = default);
}

public sealed class RoleWithPermissionsDto
{
    public string RoleName { get; init; } = string.Empty;
    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();
    public bool IsSystemRole { get; init; }
    public int UserCount { get; init; }
    /// <summary>True only for custom roles with no assigned users. Frontend uses this to enable/disable delete.</summary>
    public bool CanDelete { get; init; }
    /// <summary>False for SuperAdmin only (matrix-only). True for custom roles and other canonical roles.</summary>
    public bool CanEditPermissions { get; init; }
}

public enum SetRolePermissionsResult
{
    Success,
    RoleNotFound,
    SystemRoleNotEditable,
    InvalidPermissionKeys,
}

public enum DeleteRoleResult
{
    Success,
    RoleNotFound,
    SystemRoleNotDeletable,
    RoleHasAssignedUsers,
}
