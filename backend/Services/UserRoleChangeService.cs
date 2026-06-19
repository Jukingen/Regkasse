using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Identity;

namespace KasseAPI_Final.Services;

public sealed class UserRoleChangeService : IUserRoleChangeService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditLogService _auditLog;
    private readonly IUserSessionInvalidation _sessionInvalidation;
    private readonly ILogger<UserRoleChangeService> _logger;

    public UserRoleChangeService(
        UserManager<ApplicationUser> userManager,
        IAuditLogService auditLog,
        IUserSessionInvalidation sessionInvalidation,
        ILogger<UserRoleChangeService> logger)
    {
        _userManager = userManager;
        _auditLog = auditLog;
        _sessionInvalidation = sessionInvalidation;
        _logger = logger;
    }

    public async Task<(UserRoleChangeResult Result, string? Error)> ChangeUserRoleAsync(
        ApplicationUser user,
        string newRole,
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
            }, null);
        }

        var identityError = await ApplyIdentityRoleAsync(user, normalizedNewRole, cancellationToken).ConfigureAwait(false);
        if (identityError != null)
            return (new UserRoleChangeResult(), identityError);

        await LogRoleChangedAuditAsync(
            actorUserId,
            actorRole,
            user.Id,
            previousRole,
            normalizedNewRole,
            tenantIdForAudit,
            cancellationToken).ConfigureAwait(false);

        await _sessionInvalidation.InvalidateSessionsForUserAsync(user.Id, cancellationToken).ConfigureAwait(false);

        return (new UserRoleChangeResult
        {
            RoleChanged = true,
            PreviousRole = previousRole,
            NewRole = normalizedNewRole,
        }, null);
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

    private async Task LogRoleChangedAuditAsync(
        string actorUserId,
        string actorRole,
        string targetUserId,
        string? previousRole,
        string newRole,
        Guid? tenantIdForAudit,
        CancellationToken cancellationToken)
    {
        try
        {
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
                oldValues: new { oldRole = previousRole, role = previousRole },
                newValues: new { newRole, role = newRole }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit log failed for USER_ROLE_CHANGE user {UserId}", targetUserId);
        }

        _ = cancellationToken;
    }
}
