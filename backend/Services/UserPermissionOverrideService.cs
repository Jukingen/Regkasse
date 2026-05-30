using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class UserPermissionOverrideService : IUserPermissionOverrideService
{
    private readonly AppDbContext _db;
    private readonly IRolePermissionResolver _rolePermissionResolver;
    private readonly IEffectivePermissionResolver _effectivePermissionResolver;

    public UserPermissionOverrideService(
        AppDbContext db,
        IRolePermissionResolver rolePermissionResolver,
        IEffectivePermissionResolver effectivePermissionResolver)
    {
        _db = db;
        _rolePermissionResolver = rolePermissionResolver;
        _effectivePermissionResolver = effectivePermissionResolver;
    }

    public async Task<IReadOnlyList<UserPermissionOverrideDto>> ListOverridesAsync(
        string userId,
        Guid? tenantScope,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var query = _db.UserPermissionOverrides
            .AsNoTracking()
            .Where(o => o.UserId == userId && (o.ExpiresAt == null || o.ExpiresAt > now));

        if (tenantScope.HasValue)
            query = query.Where(o => o.TenantId == null || o.TenantId == tenantScope.Value);

        var rows = await query
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        return rows.Select(Map).ToList();
    }

    public async Task<UserEffectivePermissionsDto> GetEffectivePermissionsDetailAsync(
        string userId,
        IEnumerable<string> roleNames,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        var rolePermissions = (await _rolePermissionResolver.GetPermissionsForRolesAsync(roleNames, cancellationToken))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var overrides = await ListOverridesAsync(userId, tenantId, cancellationToken);
        var effective = (await _effectivePermissionResolver.GetEffectivePermissionsAsync(userId, roleNames, tenantId, cancellationToken))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new UserEffectivePermissionsDto
        {
            RolePermissions = rolePermissions,
            Overrides = overrides,
            EffectivePermissions = effective,
        };
    }

    public async Task<UserPermissionOverrideDto?> UpsertOverrideAsync(
        string targetUserId,
        UpsertUserPermissionOverrideRequest request,
        string actorUserId,
        Guid? actorTenantScope,
        CancellationToken cancellationToken = default)
    {
        var permission = request.Permission?.Trim();
        if (string.IsNullOrWhiteSpace(permission) || !PermissionCatalogMetadata.IsValidPermissionKey(permission))
            return null;

        if (actorTenantScope.HasValue && request.TenantId.HasValue && request.TenantId != actorTenantScope)
            return null;

        var tenantId = request.TenantId ?? actorTenantScope;
        var existing = await _db.UserPermissionOverrides
            .Where(o => o.UserId == targetUserId
                && o.Permission == permission
                && o.TenantId == tenantId)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        UserPermissionOverride entity;
        if (existing != null)
        {
            existing.IsGranted = request.IsGranted;
            existing.Reason = request.Reason?.Trim();
            existing.ExpiresAt = request.ExpiresAt;
            existing.CreatedAt = DateTime.UtcNow;
            existing.CreatedByUserId = actorUserId;
            entity = existing;
        }
        else
        {
            entity = new UserPermissionOverride
            {
                UserId = targetUserId,
                TenantId = tenantId,
                Permission = permission,
                IsGranted = request.IsGranted,
                Reason = request.Reason?.Trim(),
                ExpiresAt = request.ExpiresAt,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = actorUserId,
            };
            _db.UserPermissionOverrides.Add(entity);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<bool> DeleteOverrideAsync(
        string targetUserId,
        Guid overrideId,
        Guid? actorTenantScope,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.UserPermissionOverrides
            .FirstOrDefaultAsync(o => o.Id == overrideId && o.UserId == targetUserId, cancellationToken);
        if (entity == null)
            return false;

        if (actorTenantScope.HasValue && entity.TenantId.HasValue && entity.TenantId != actorTenantScope)
            return false;

        _db.UserPermissionOverrides.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static UserPermissionOverrideDto Map(UserPermissionOverride entity) => new()
    {
        Id = entity.Id,
        UserId = entity.UserId,
        TenantId = entity.TenantId,
        Permission = entity.Permission,
        IsGranted = entity.IsGranted,
        Reason = entity.Reason,
        CreatedAt = entity.CreatedAt,
        CreatedByUserId = entity.CreatedByUserId,
        ExpiresAt = entity.ExpiresAt,
    };
}
