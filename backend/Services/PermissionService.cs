using KasseAPI_Final.Auth;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <summary>
/// Permission checks and override CRUD. Uses <see cref="RolePermissionMatrix"/> for roles,
/// <see cref="UserPermissionOverride"/> rows for grants/denies, and <see cref="PermissionImplication"/> for composite keys.
/// </summary>
public sealed class PermissionService : IPermissionService
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEffectivePermissionResolver _effectivePermissionResolver;
    private readonly IAuditLogService _auditLogService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public PermissionService(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        IEffectivePermissionResolver effectivePermissionResolver,
        IAuditLogService auditLogService,
        IHttpContextAccessor httpContextAccessor,
        ICurrentTenantAccessor tenantAccessor)
    {
        _context = context;
        _userManager = userManager;
        _effectivePermissionResolver = effectivePermissionResolver;
        _auditLogService = auditLogService;
        _httpContextAccessor = httpContextAccessor;
        _tenantAccessor = tenantAccessor;
    }

    public async Task<bool> HasPermissionAsync(
        string userId,
        string permission,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(permission))
            return false;

        if (!PermissionCatalogMetadata.IsValidPermissionKey(permission))
            return false;

        if (await IsSuperAdminAsync(userId, cancellationToken))
            return true;

        var resolvedTenantId = tenantId ?? _tenantAccessor.TenantId;
        var roleNames = await GetUserRoleNamesAsync(userId, cancellationToken);
        var effective = await _effectivePermissionResolver.GetEffectivePermissionsAsync(
            userId,
            roleNames,
            resolvedTenantId,
            cancellationToken);

        return PermissionImplication.IsSatisfied(permission, effective);
    }

    public async Task<IReadOnlyList<UserPermissionOverride>> GetUserOverridesAsync(
        string userId,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Array.Empty<UserPermissionOverride>();

        var resolvedTenantId = tenantId ?? _tenantAccessor.TenantId;
        var now = DateTime.UtcNow;

        var query = _context.UserPermissionOverrides
            .AsNoTracking()
            .Where(o => o.UserId == userId
                && (o.ValidFrom == null || o.ValidFrom <= now)
                && (o.ExpiresAt == null || o.ExpiresAt > now));

        if (resolvedTenantId.HasValue)
            query = query.Where(o => o.TenantId == null || o.TenantId == resolvedTenantId.Value);

        return await query
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddOrUpdatePermissionOverrideAsync(
        string userId,
        string permission,
        bool isGranted,
        string? reason,
        DateTime? expiresAt,
        Guid? tenantId = null,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPermission = permission?.Trim();
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(normalizedPermission))
            throw new ArgumentException("User id and permission are required.");

        if (!PermissionCatalogMetadata.IsValidPermissionKey(normalizedPermission))
            throw new ArgumentException($"Unknown permission key: {normalizedPermission}", nameof(permission));

        var resolvedTenantId = tenantId ?? _tenantAccessor.TenantId;
        var existing = await _context.UserPermissionOverrides
            .Where(o => o.UserId == userId
                && o.Permission == normalizedPermission
                && o.TenantId == resolvedTenantId)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var actorId = actorUserId ?? _httpContextAccessor.HttpContext?.User.GetActorUserId() ?? userId;
        var actorRole = _httpContextAccessor.HttpContext?.User.GetActorRole() ?? "System";

        if (existing != null)
        {
            existing.IsGranted = isGranted;
            existing.Reason = reason?.Trim();
            existing.ExpiresAt = expiresAt;
            existing.CreatedAt = DateTime.UtcNow;
            existing.CreatedByUserId = actorId;
        }
        else
        {
            _context.UserPermissionOverrides.Add(new UserPermissionOverride
            {
                UserId = userId,
                TenantId = resolvedTenantId,
                Permission = normalizedPermission,
                IsGranted = isGranted,
                Reason = reason?.Trim(),
                ExpiresAt = expiresAt,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = actorId,
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogUserLifecycleAsync(
            AuditEventType.UserPermissionOverridesChanged,
            actorId,
            actorRole,
            userId,
            reason: reason,
            status: AuditLogStatus.Success,
            description: $"Permission {normalizedPermission} for user {userId} set to {isGranted}",
            oldValues: null,
            newValues: new
            {
                Permission = normalizedPermission,
                IsGranted = isGranted,
                TenantId = resolvedTenantId,
                ExpiresAt = expiresAt,
                Reason = reason,
            });
    }

    private async Task<bool> IsSuperAdminAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return false;

        var roles = await _userManager.GetRolesAsync(user);
        foreach (var role in roles)
        {
            if (string.Equals(RoleCanonicalization.GetCanonicalRole(role), Roles.SuperAdmin, StringComparison.Ordinal))
                return true;
        }

        return string.Equals(RoleCanonicalization.GetCanonicalRole(user.Role), Roles.SuperAdmin, StringComparison.Ordinal);
    }

    private async Task<IReadOnlyList<string>> GetUserRoleNamesAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Array.Empty<string>();

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Count > 0)
            return roles.ToList();

        return string.IsNullOrWhiteSpace(user.Role)
            ? Array.Empty<string>()
            : new[] { user.Role.Trim() };
    }
}
