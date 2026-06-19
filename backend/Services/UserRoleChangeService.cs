using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class UserRoleChangeService : IUserRoleChangeService
{
    private const string PreserveOverrideReasonPrefix = "Role change: permission preserved from previous role";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRolePermissionResolver _rolePermissionResolver;
    private readonly IUserPermissionOverrideService _permissionOverrideService;
    private readonly IAuditLogService _auditLog;
    private readonly IUserSessionInvalidation _sessionInvalidation;
    private readonly AppDbContext _db;
    private readonly ILogger<UserRoleChangeService> _logger;

    public UserRoleChangeService(
        UserManager<ApplicationUser> userManager,
        IRolePermissionResolver rolePermissionResolver,
        IUserPermissionOverrideService permissionOverrideService,
        IAuditLogService auditLog,
        IUserSessionInvalidation sessionInvalidation,
        AppDbContext db,
        ILogger<UserRoleChangeService> logger)
    {
        _userManager = userManager;
        _rolePermissionResolver = rolePermissionResolver;
        _permissionOverrideService = permissionOverrideService;
        _auditLog = auditLog;
        _sessionInvalidation = sessionInvalidation;
        _db = db;
        _logger = logger;
    }

    public async Task<(UserRoleChangeResult Result, string? Error)> ChangeUserRoleAsync(
        ApplicationUser user,
        string newRole,
        bool preservePreviousPermissions,
        string actorUserId,
        string actorRole,
        Guid? tenantIdForAudit,
        CancellationToken cancellationToken = default)
    {
        var normalizedNewRole = newRole.Trim();
        var previousRole = user.Role?.Trim();

        if (string.Equals(previousRole, normalizedNewRole, StringComparison.OrdinalIgnoreCase))
        {
            return (new UserRoleChangeResult
            {
                RoleChanged = false,
                PreviousRole = previousRole,
                NewRole = normalizedNewRole,
                PreservePreviousPermissions = false,
            }, null);
        }

        var effectivePreserve = ShouldPreservePermissions(preservePreviousPermissions, previousRole, normalizedNewRole);

        IReadOnlySet<string> previousRolePermissions = Array.Empty<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (effectivePreserve && !string.IsNullOrWhiteSpace(previousRole))
        {
            previousRolePermissions = await ResolveRolePermissionsAsync(previousRole, cancellationToken)
                .ConfigureAwait(false);
        }

        var identityError = await ApplyIdentityRoleAsync(user, normalizedNewRole, cancellationToken).ConfigureAwait(false);
        if (identityError != null)
            return (new UserRoleChangeResult(), identityError);

        var overridesCreatedOrUpdated = 0;
        if (effectivePreserve && !string.IsNullOrWhiteSpace(previousRole))
        {
            var newRolePermissions = await ResolveRolePermissionsAsync(normalizedNewRole, cancellationToken)
                .ConfigureAwait(false);
            var merged = RolePermissionMatrix.MergePermissions(previousRolePermissions, newRolePermissions);
            var permissionsToPreserve = merged
                .Where(p => !newRolePermissions.Contains(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            overridesCreatedOrUpdated = await ApplyPreserveOverridesAsync(
                user.Id,
                permissionsToPreserve,
                previousRole!,
                actorUserId,
                cancellationToken).ConfigureAwait(false);
        }

        await LogRoleChangedAuditAsync(
            actorUserId,
            actorRole,
            user.Id,
            previousRole,
            normalizedNewRole,
            effectivePreserve,
            overridesCreatedOrUpdated,
            tenantIdForAudit,
            cancellationToken).ConfigureAwait(false);

        await _sessionInvalidation.InvalidateSessionsForUserAsync(user.Id, cancellationToken).ConfigureAwait(false);

        return (new UserRoleChangeResult
        {
            RoleChanged = true,
            PreviousRole = previousRole,
            NewRole = normalizedNewRole,
            PreservePreviousPermissions = effectivePreserve,
            OverridesCreatedOrUpdated = overridesCreatedOrUpdated,
        }, null);
    }

    private static bool ShouldPreservePermissions(
        bool preservePreviousPermissions,
        string? previousRole,
        string newRole)
    {
        if (!preservePreviousPermissions || string.IsNullOrWhiteSpace(previousRole))
            return false;

        if (string.Equals(previousRole, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.Equals(newRole, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    private async Task<IReadOnlySet<string>> ResolveRolePermissionsAsync(
        string roleName,
        CancellationToken cancellationToken)
    {
        return await _rolePermissionResolver.GetPermissionsForRolesAsync(new[] { roleName }, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<string?> ApplyIdentityRoleAsync(
        ApplicationUser user,
        string normalizedRole,
        CancellationToken cancellationToken)
    {
        var previousRoles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
        if (previousRoles.Count > 0)
        {
            var remove = await _userManager.RemoveFromRolesAsync(user, previousRoles).ConfigureAwait(false);
            if (!remove.Succeeded)
                return string.Join("; ", remove.Errors.Select(e => e.Description));
        }

        var add = await _userManager.AddToRoleAsync(user, normalizedRole).ConfigureAwait(false);
        if (!add.Succeeded)
            return string.Join("; ", add.Errors.Select(e => e.Description));

        user.Role = normalizedRole;
        user.UpdatedAt = DateTime.UtcNow;
        var update = await _userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!update.Succeeded)
            return string.Join("; ", update.Errors.Select(e => e.Description));

        _ = cancellationToken;
        return null;
    }

    private async Task<int> ApplyPreserveOverridesAsync(
        string targetUserId,
        IReadOnlyList<string> permissionsToPreserve,
        string previousRole,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        if (permissionsToPreserve.Count == 0)
            return 0;

        var now = DateTime.UtcNow;
        var activeDenies = await _db.UserPermissionOverrides
            .AsNoTracking()
            .Where(o => o.UserId == targetUserId
                && !o.IsGranted
                && (o.ExpiresAt == null || o.ExpiresAt > now))
            .Select(o => o.Permission)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var denySet = new HashSet<string>(activeDenies, StringComparer.OrdinalIgnoreCase);

        var reason = $"{PreserveOverrideReasonPrefix} ({previousRole})";
        var count = 0;

        foreach (var permission in permissionsToPreserve)
        {
            if (!PermissionCatalogMetadata.IsValidPermissionKey(permission))
                continue;
            if (denySet.Contains(permission))
                continue;

            var upserted = await _permissionOverrideService.UpsertOverrideAsync(
                targetUserId,
                new UpsertUserPermissionOverrideRequest
                {
                    Permission = permission,
                    IsGranted = true,
                    Reason = reason,
                    TenantId = null,
                },
                actorUserId,
                actorTenantScope: null,
                cancellationToken).ConfigureAwait(false);

            if (upserted != null)
                count++;
        }

        return count;
    }

    private async Task LogRoleChangedAuditAsync(
        string actorUserId,
        string actorRole,
        string targetUserId,
        string? previousRole,
        string newRole,
        bool preservePreviousPermissions,
        int overridesCreatedOrUpdated,
        Guid? tenantIdForAudit,
        CancellationToken cancellationToken)
    {
        try
        {
            var oldValues = new
            {
                oldRole = previousRole,
                role = previousRole,
            };
            var newValues = new
            {
                newRole,
                role = newRole,
                preservedPermissions = preservePreviousPermissions,
                overridesCreatedOrUpdated,
            };

            await _auditLog.LogUserLifecycleAsync(
                AuditEventType.UserRoleChanged,
                actorUserId,
                actorRole,
                targetUserId,
                tenantIdForAudit,
                reason: $"Role changed from {previousRole} to {newRole}",
                correlationId: null,
                AuditLogStatus.Success,
                description: $"Role change: {previousRole} -> {newRole}",
                oldValues: oldValues,
                newValues: newValues).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Audit log failed for USER_ROLE_CHANGE user {UserId}",
                targetUserId);
        }

        _ = cancellationToken;
    }
}
