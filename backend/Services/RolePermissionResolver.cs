using System.Security.Claims;
using KasseAPI_Final.Authorization;
using Microsoft.AspNetCore.Identity;

namespace KasseAPI_Final.Services;

/// <summary>
/// Resolves permissions: system roles from RolePermissionMatrix, custom roles from AspNetRoleClaims.
/// RolePermissionMatrix behavior is unchanged; custom roles use stored claims only.
/// </summary>
public sealed class RolePermissionResolver : IRolePermissionResolver
{
    private readonly RoleManager<IdentityRole> _roleManager;

    public RolePermissionResolver(RoleManager<IdentityRole> roleManager)
    {
        _roleManager = roleManager;
    }

    public async Task<IReadOnlySet<string>> GetPermissionsForRolesAsync(
        IEnumerable<string> roleNames,
        CancellationToken cancellationToken = default)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (roleNames == null) return result;

        foreach (var roleName in roleNames)
        {
            if (string.IsNullOrWhiteSpace(roleName)) continue;

            if (IsSystemRole(roleName))
            {
                var fromMatrix = RolePermissionMatrix.GetPermissionsForRoles(new[] { roleName });
                foreach (var p in fromMatrix) result.Add(p);
            }
            else
            {
                var role = await _roleManager.FindByNameAsync(roleName);
                if (role == null) continue;

                var claims = await _roleManager.GetClaimsAsync(role);
                foreach (var c in claims)
                {
                    if (string.Equals(c.Type, PermissionCatalog.PermissionClaimType, StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrEmpty(c.Value))
                        result.Add(c.Value);
                }
            }
        }

        return result;
    }

    private static bool IsSystemRole(string roleName)
    {
        return Roles.Canonical.Contains(roleName, StringComparer.OrdinalIgnoreCase);
    }
}
