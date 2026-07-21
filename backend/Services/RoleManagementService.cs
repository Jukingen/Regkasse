using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <summary>
/// Implements role catalog, list with permissions, set permissions (custom only), delete (custom only, no assigned users).
/// Permission update: custom roles via claims only. Canonical (system) roles are matrix-only and
/// cannot be edited at runtime; SuperAdmin can assign system roles and manage custom roles only.
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
            // All system roles are immutable at runtime: permissions come from RolePermissionMatrix only.
            var canEdit = !isSystem;
            var permissionList = permissions.ToList();
            result.Add(new RoleWithPermissionsDto
            {
                RoleName = name,
                RoleKey = RoleMetadata.GetRoleKey(name),
                DisplayName = RoleMetadata.GetDisplayName(name),
                Description = RoleMetadata.GetDescription(name),
                Permissions = permissionList,
                IsSystemRole = isSystem,
                IsImmutable = isSystem,
                UserCount = count,
                CanDelete = !isSystem && count == 0,
                CanEditPermissions = canEdit,
                UiCapabilities = new UiCapabilitiesDto
                {
                    PosLogin = ClientAppPolicy.CanLoginToPos(name),
                    AdminLogin = ClientAppPolicy.CanLoginToAdmin(name),
                },
                PermissionGroups = BuildPermissionGroups(permissionList),
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
            if (string.IsNullOrWhiteSpace(key))
                continue;
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

        // Delete guard: never remove a role still assigned — users must not be left without a role in our single-role model.
        var usersInRole = await _userManager.GetUsersInRoleAsync(roleName);
        if (usersInRole != null && usersInRole.Count > 0)
            return DeleteRoleResult.RoleHasAssignedUsers;

        var result = await _roleManager.DeleteAsync(role);
        if (!result.Succeeded)
            return DeleteRoleResult.RoleHasAssignedUsers; // or another failure; Identity often fails delete if users in role

        return DeleteRoleResult.Success;
    }

    public async Task<CreateRoleResult> CreateRoleAsync(
        string roleName,
        string? inheritFromRole,
        CancellationToken cancellationToken = default)
    {
        var trimmedName = roleName?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmedName))
            return CreateRoleResult.Failed;

        if (Roles.Canonical.Contains(trimmedName, StringComparer.OrdinalIgnoreCase)
            || Roles.ReservedRoleNames.Contains(trimmedName, StringComparer.OrdinalIgnoreCase))
        {
            return CreateRoleResult.ReservedName;
        }

        if (await _roleManager.FindByNameAsync(trimmedName).ConfigureAwait(false) != null)
            return CreateRoleResult.RoleAlreadyExists;

        var sourceRole = inheritFromRole?.Trim();
        IReadOnlyCollection<string>? permissionsToCopy = null;
        if (!string.IsNullOrEmpty(sourceRole))
        {
            if (string.Equals(sourceRole, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
                return CreateRoleResult.CannotInheritFromSuperAdmin;

            if (string.Equals(sourceRole, trimmedName, StringComparison.OrdinalIgnoreCase))
                return CreateRoleResult.SourceRoleNotFound;

            var sourceExists = await _roleManager.FindByNameAsync(sourceRole).ConfigureAwait(false) != null
                || Roles.Canonical.Contains(sourceRole, StringComparer.OrdinalIgnoreCase);
            if (!sourceExists)
                return CreateRoleResult.SourceRoleNotFound;

            permissionsToCopy = await _resolver.GetPermissionsForRolesAsync(new[] { sourceRole }, cancellationToken)
                .ConfigureAwait(false);
        }

        var create = await _roleManager.CreateAsync(new IdentityRole(trimmedName)).ConfigureAwait(false);
        if (!create.Succeeded)
            return CreateRoleResult.Failed;

        if (permissionsToCopy == null || permissionsToCopy.Count == 0)
            return CreateRoleResult.Success;

        var setResult = await SetRolePermissionsAsync(trimmedName, permissionsToCopy.ToList(), cancellationToken)
            .ConfigureAwait(false);
        if (setResult == SetRolePermissionsResult.Success)
            return CreateRoleResult.Success;

        var createdRole = await _roleManager.FindByNameAsync(trimmedName).ConfigureAwait(false);
        if (createdRole != null)
            await _roleManager.DeleteAsync(createdRole).ConfigureAwait(false);

        return CreateRoleResult.Failed;
    }

    /// <summary>
    /// System role behavior is defined solely by Roles.Canonical membership (case-insensitive).
    /// Any name in that list is immutable at runtime, matrix-only for permissions, and not deletable.
    /// </summary>
    private static bool IsSystemRole(string roleName)
    {
        return Roles.Canonical.Contains(roleName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Groups permission keys by catalog group (groupKey slug) for Role Capability Matrix response.
    /// </summary>
    private static IReadOnlyList<PermissionGroupDto> BuildPermissionGroups(IReadOnlyList<string> permissionKeys)
    {
        if (permissionKeys == null || permissionKeys.Count == 0)
            return Array.Empty<PermissionGroupDto>();

        var byGroup = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in permissionKeys)
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;
            var groupKey = PermissionCatalogMetadata.GetGroupKeyForPermission(key);
            if (!byGroup.TryGetValue(groupKey, out var list))
            {
                list = new List<string>();
                byGroup[groupKey] = list;
            }
            list.Add(key);
        }

        return byGroup
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new PermissionGroupDto { GroupKey = kv.Key, Permissions = kv.Value })
            .ToList();
    }
}
