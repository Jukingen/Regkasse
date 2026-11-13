using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Identity;

namespace KasseAPI_Final.Services;

public sealed class RolePermissionSimulateService : IRolePermissionSimulateService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRolePermissionResolver _rolePermissionResolver;
    private readonly IEffectivePermissionResolver _effectivePermissionResolver;

    public RolePermissionSimulateService(
        UserManager<ApplicationUser> userManager,
        IRolePermissionResolver rolePermissionResolver,
        IEffectivePermissionResolver effectivePermissionResolver)
    {
        _userManager = userManager;
        _rolePermissionResolver = rolePermissionResolver;
        _effectivePermissionResolver = effectivePermissionResolver;
    }

    public async Task<RolePermissionSimulateResultDto> SimulateAsync(
        string roleName,
        IReadOnlyList<string> proposedPermissions,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var proposed = (proposedPermissions ?? Array.Empty<string>())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var current = await _rolePermissionResolver.GetPermissionsForRolesAsync(
            new[] { roleName },
            cancellationToken);

        var added = proposed.Except(current, StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var removed = current.Except(proposed, StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var usersInRole = await _userManager.GetUsersInRoleAsync(roleName);
        var ordered = usersInRole
            .OrderBy(u => u.UserName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var impacts = new List<RolePermissionSimulateUserImpactDto>();
        foreach (var user in ordered)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var before = await _effectivePermissionResolver.GetEffectivePermissionsAsync(
                user.Id,
                roles,
                tenantId: null,
                cancellationToken);
            var after = await _effectivePermissionResolver.GetEffectivePermissionsWithRoleOverrideAsync(
                user.Id,
                proposed,
                tenantId: null,
                cancellationToken);

            var gained = after.Except(before, StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var lost = before.Except(after, StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();

            impacts.Add(new RolePermissionSimulateUserImpactDto
            {
                UserId = user.Id,
                UserName = user.UserName ?? user.Id,
                DisplayRole = roles.FirstOrDefault() ?? roleName,
                PermissionsGained = gained.Count,
                PermissionsLost = lost.Count,
                GainedKeysSample = gained.Take(10).ToList(),
                LostKeysSample = lost.Take(10).ToList(),
            });
        }

        var total = impacts.Count;
        var pageItems = impacts
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new RolePermissionSimulateResultDto
        {
            RoleName = roleName,
            CurrentPermissions = current.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList(),
            ProposedPermissions = proposed.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList(),
            Added = added,
            Removed = removed,
            AffectedUserCount = impacts.Count(i => i.PermissionsGained > 0 || i.PermissionsLost > 0),
            Users = pageItems,
            Page = page,
            PageSize = pageSize,
            Total = total,
        };
    }
}
