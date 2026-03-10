using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <summary>
/// Implements role catalog, list with permissions, set permissions (custom only), delete (custom only, no assigned users).
/// Permission update is explicitly limited to custom roles; system roles are read-only.
/// </summary>
public sealed class RoleManagementService : IRoleManagementService
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRolePermissionResolver _resolver;

    public RoleManagementService(
        RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IRolePermissionResolver resolver)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _resolver = resolver;
    }

    public IReadOnlyList<PermissionCatalogMetadata.Item> GetPermissionsCatalog()
    {
        return PermissionCatalogMetadata.GetAll();
    }

    public async Task<IReadOnlyList<RoleWithPermissionsDto>> GetRolesWithPermissionsAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _roleManager.Roles.ToListAsync(cancellationToken);
        var result = new List<RoleWithPermissionsDto>();

        foreach (var role in roles)
        {
            var name = role.Name ?? string.Empty;
            var isSystem = IsSystemRole(name);
            var permissions = await _resolver.GetPermissionsForRolesAsync(new[] { name }, cancellationToken);
            var userCount = await _userManager.GetUsersInRoleAsync(name);
            var count = userCount?.Count ?? 0;
            result.Add(new RoleWithPermissionsDto
            {
                RoleName = name,
                Permissions = permissions.ToList(),
                IsSystemRole = isSystem,
                UserCount = count,
                CanDelete = !isSystem && count == 0,
                CanEditPermissions = !isSystem,
            });
        }

        return result;
    }

    public async Task<SetRolePermissionsResult> SetRolePermissionsAsync(string roleName, IReadOnlyList<string> permissionKeys, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roleName))
            return SetRolePermissionsResult.RoleNotFound;

        var role = await _roleManager.FindByNameAsync(roleName);
        if (role == null)
            return SetRolePermissionsResult.RoleNotFound;

        if (IsSystemRole(roleName))
            return SetRolePermissionsResult.SystemRoleNotEditable;

        if (permissionKeys != null)
        {
            foreach (var key in permissionKeys)
            {
                if (!PermissionCatalogMetadata.IsValidPermissionKey(key))
                    return SetRolePermissionsResult.InvalidPermissionKeys;
            }
        }

        var currentClaims = (await _roleManager.GetClaimsAsync(role))
            .Where(c => string.Equals(c.Type, PermissionCatalog.PermissionClaimType, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var claim in currentClaims)
            await _roleManager.RemoveClaimAsync(role, claim);

        var keys = permissionKeys ?? Array.Empty<string>();
        foreach (var key in keys.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            await _roleManager.AddClaimAsync(role, new Claim(PermissionCatalog.PermissionClaimType, key));
        }

        return SetRolePermissionsResult.Success;
    }

    public async Task<DeleteRoleResult> DeleteRoleAsync(string roleName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roleName))
            return DeleteRoleResult.RoleNotFound;

        var role = await _roleManager.FindByNameAsync(roleName);
        if (role == null)
            return DeleteRoleResult.RoleNotFound;

        if (IsSystemRole(roleName))
            return DeleteRoleResult.SystemRoleNotDeletable;

        var usersInRole = await _userManager.GetUsersInRoleAsync(roleName);
        if (usersInRole != null && usersInRole.Count > 0)
            return DeleteRoleResult.RoleHasAssignedUsers;

        var result = await _roleManager.DeleteAsync(role);
        if (!result.Succeeded)
            return DeleteRoleResult.RoleHasAssignedUsers; // or another failure; Identity often fails delete if users in role

        return DeleteRoleResult.Success;
    }

    private static bool IsSystemRole(string roleName)
    {
        return Roles.Canonical.Contains(roleName, StringComparer.OrdinalIgnoreCase);
    }
}
