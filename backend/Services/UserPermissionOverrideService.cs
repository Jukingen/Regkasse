using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

public sealed class UserPermissionOverrideService : IUserPermissionOverrideService
{
    private readonly AppDbContext _db;
    private readonly IRolePermissionResolver _rolePermissionResolver;
    private readonly IEffectivePermissionResolver _effectivePermissionResolver;
    private readonly TemporaryPermissionsOptions _options;

    public UserPermissionOverrideService(
        AppDbContext db,
        IRolePermissionResolver rolePermissionResolver,
        IEffectivePermissionResolver effectivePermissionResolver,
        IOptions<TemporaryPermissionsOptions> options)
    {
        _db = db;
        _rolePermissionResolver = rolePermissionResolver;
        _effectivePermissionResolver = effectivePermissionResolver;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<UserPermissionOverrideDto>> ListOverridesAsync(
        string userId,
        Guid? tenantScope,
        bool includeExpired = false,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var query = _db.UserPermissionOverrides
            .AsNoTracking()
            .Where(o => o.UserId == userId);

        if (!includeExpired)
        {
            query = query.Where(o =>
                (o.ValidFrom == null || o.ValidFrom <= now)
                && (o.ExpiresAt == null || o.ExpiresAt > now));
        }

        if (tenantScope.HasValue)
            query = query.Where(o => o.TenantId == null || o.TenantId == tenantScope.Value);

        var rows = await query
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        return rows.Select(e => Map(e, now)).ToList();
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
        var overrides = await ListOverridesAsync(userId, tenantId, includeExpired: false, cancellationToken);
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

        if (request.ValidFrom.HasValue && request.ExpiresAt.HasValue && request.ValidFrom >= request.ExpiresAt)
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
            existing.ValidFrom = request.ValidFrom;
            existing.ExpiresAt = request.ExpiresAt;
            existing.CreatedAt = DateTime.UtcNow;
            existing.CreatedByUserId = actorUserId;
            existing.ExpiringNotifiedAt = null;
            existing.ExpiredProcessedAt = null;
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
                ValidFrom = request.ValidFrom,
                ExpiresAt = request.ExpiresAt,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = actorUserId,
            };
            _db.UserPermissionOverrides.Add(entity);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Map(entity, DateTime.UtcNow);
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

    private UserPermissionOverrideDto Map(UserPermissionOverride entity, DateTime utcNow) => new()
    {
        Id = entity.Id,
        UserId = entity.UserId,
        TenantId = entity.TenantId,
        Permission = entity.Permission,
        IsGranted = entity.IsGranted,
        Reason = entity.Reason,
        CreatedAt = entity.CreatedAt,
        CreatedByUserId = entity.CreatedByUserId,
        ValidFrom = entity.ValidFrom,
        ExpiresAt = entity.ExpiresAt,
        Status = UserPermissionOverrideStatuses.Compute(
            entity.ValidFrom,
            entity.ExpiresAt,
            utcNow,
            _options.ExpiringSoonHours),
    };
}
