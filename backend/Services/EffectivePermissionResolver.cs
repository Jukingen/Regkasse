using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class EffectivePermissionResolver : IEffectivePermissionResolver
{
    private readonly AppDbContext _db;
    private readonly IRolePermissionResolver _rolePermissionResolver;

    public EffectivePermissionResolver(AppDbContext db, IRolePermissionResolver rolePermissionResolver)
    {
        _db = db;
        _rolePermissionResolver = rolePermissionResolver;
    }

    public async Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(
        string userId,
        IEnumerable<string> roleNames,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var result = new HashSet<string>(
            await _rolePermissionResolver.GetPermissionsForRolesAsync(roleNames, cancellationToken),
            StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(userId))
            return result;

        var now = DateTime.UtcNow;
        var overrides = await _db.UserPermissionOverrides
            .AsNoTracking()
            .Where(o => o.UserId == userId
                && (o.ExpiresAt == null || o.ExpiresAt > now)
                && (o.TenantId == null || o.TenantId == tenantId))
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        foreach (var group in overrides.GroupBy(o => o.Permission, StringComparer.OrdinalIgnoreCase))
        {
            var latest = group.First();
            if (!PermissionCatalogMetadata.IsValidPermissionKey(latest.Permission))
                continue;

            if (latest.IsGranted)
                result.Add(latest.Permission);
            else
                result.Remove(latest.Permission);
        }

        return result;
    }
}
