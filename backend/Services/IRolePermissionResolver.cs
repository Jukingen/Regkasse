namespace KasseAPI_Final.Services;

/// <summary>
/// Resolves effective permissions for a set of roles.
/// Deterministic model: system roles use RolePermissionMatrix (code-only); custom roles use AspNetRoleClaims (permission claim type).
/// Used by TokenClaimsService so JWT permission claims include both system and custom role permissions.
/// </summary>
public interface IRolePermissionResolver
{
    /// <summary>
    /// Returns the union of permissions for the given roles.
    /// System roles: permissions from RolePermissionMatrix (unchanged).
    /// Custom roles: permissions from AspNetRoleClaims where ClaimType = permission.
    /// </summary>
    Task<IReadOnlySet<string>> GetPermissionsForRolesAsync(
        IEnumerable<string> roleNames,
        CancellationToken cancellationToken = default);
}
