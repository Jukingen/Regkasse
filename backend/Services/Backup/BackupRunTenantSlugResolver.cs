using System.Text.RegularExpressions;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Resolves a filesystem-safe tenant slug label for backup artifact names from run metadata.
/// Prefers <see cref="BackupRun.TenantId"/>; falls back to <see cref="BackupRun.IdempotencyKey"/> encoding.
/// </summary>
public static partial class BackupRunTenantSlugResolver
{
    public const string AllTenantsSlug = "all";
    public const string ScheduledSlug = "scheduled";
    public const string DeploymentSlug = "deployment";

    public static async Task<string> ResolveSlugAsync(
        BackupRun run,
        AppDbContext db,
        CancellationToken cancellationToken = default)
    {
        if (run.TenantId is Guid columnTenantId && columnTenantId != Guid.Empty)
        {
            var slugFromColumn = await db.Tenants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(t => t.Id == columnTenantId)
                .Select(t => t.Slug)
                .FirstOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(slugFromColumn))
                return slugFromColumn.Trim();
        }

        var key = run.IdempotencyKey?.Trim();
        if (!string.IsNullOrEmpty(key))
        {
            if (key.StartsWith("manual-all-tenants", StringComparison.OrdinalIgnoreCase))
                return AllTenantsSlug;

            if (TryParseTenantIdFromIdempotencyKey(key, out var tenantId))
            {
                var slug = await db.Tenants
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(t => t.Id == tenantId)
                    .Select(t => t.Slug)
                    .FirstOrDefaultAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(slug))
                    return slug.Trim();
            }
        }

        if (run.TriggerSource == BackupTriggerSource.Scheduled)
            return ScheduledSlug;

        return DeploymentSlug;
    }

    /// <summary>Tenant-scoped runs via <see cref="BackupRun.TenantId"/> or legacy idempotency key.</summary>
    public static IQueryable<BackupRun> ApplyTenantHint(IQueryable<BackupRun> query, Guid? tenantId)
    {
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
            return query;

        return BackupRunAccessEvaluator.ApplyTenantScopeFilter(query, tenantId.Value);
    }

    /// <summary>Whether <paramref name="run"/> belongs to <paramref name="tenantId"/>.</summary>
    public static bool MatchesTenantHint(BackupRun run, Guid tenantId)
    {
        if (run.TenantId is Guid columnTenantId && columnTenantId != Guid.Empty)
            return columnTenantId == tenantId;

        var key = run.IdempotencyKey;
        if (string.IsNullOrWhiteSpace(key))
            return false;

        var lower = key.ToLowerInvariant();
        var manualNeedle = $"manual-tenant-{tenantId}".ToLowerInvariant();
        if (lower.Contains(manualNeedle))
            return true;

        var importNeedle = $"import-tenant-{tenantId}".ToLowerInvariant();
        return lower.Contains(importNeedle);
    }

    /// <inheritdoc cref="BackupRunAccessEvaluator.ApplyCallerAccessFilter"/>
    public static IQueryable<BackupRun> ApplyCallerAccessFilter(
        IQueryable<BackupRun> query,
        BackupRunAccessScope scope) =>
        BackupRunAccessEvaluator.ApplyCallerAccessFilter(query, scope);

    /// <summary>Whether the key already scopes a tenant (manual/import prefix or all-tenants marker).</summary>
    public static bool EncodesTenantScope(string? idempotencyKey)
    {
        var key = idempotencyKey?.Trim();
        if (string.IsNullOrEmpty(key))
            return false;

        if (key.StartsWith("manual-all-tenants", StringComparison.OrdinalIgnoreCase))
            return true;

        return TryParseTenantIdFromIdempotencyKey(key, out _);
    }

    public static bool TryParseTenantIdFromIdempotencyKey(string? idempotencyKey, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        var key = idempotencyKey?.Trim();
        if (string.IsNullOrEmpty(key))
            return false;

        var manualTenant = ManualTenantIdempotencyKey().Match(key);
        if (manualTenant.Success && Guid.TryParse(manualTenant.Groups[1].Value, out tenantId))
            return true;

        var importTenant = ImportTenantIdempotencyKey().Match(key);
        return importTenant.Success && Guid.TryParse(importTenant.Groups[1].Value, out tenantId);
    }

    public static string ResolveSlug(BackupRun run, IReadOnlyDictionary<Guid, string> slugByTenantId)
    {
        if (run.TenantId is Guid columnTenantId
            && slugByTenantId.TryGetValue(columnTenantId, out var columnSlug)
            && !string.IsNullOrWhiteSpace(columnSlug))
        {
            return columnSlug.Trim();
        }

        var key = run.IdempotencyKey?.Trim();
        if (!string.IsNullOrEmpty(key))
        {
            if (key.StartsWith("manual-all-tenants", StringComparison.OrdinalIgnoreCase))
                return AllTenantsSlug;

            if (TryParseTenantIdFromIdempotencyKey(key, out var tenantId)
                && slugByTenantId.TryGetValue(tenantId, out var slug)
                && !string.IsNullOrWhiteSpace(slug))
            {
                return slug.Trim();
            }
        }

        if (run.TriggerSource == BackupTriggerSource.Scheduled)
            return ScheduledSlug;

        return DeploymentSlug;
    }

    public static async Task<IReadOnlyDictionary<Guid, string>> LoadSlugByTenantIdAsync(
        IEnumerable<BackupRun> runs,
        AppDbContext db,
        CancellationToken cancellationToken = default)
    {
        var tenantIds = new HashSet<Guid>();
        foreach (var run in runs)
        {
            if (run.TenantId is Guid columnTenantId && columnTenantId != Guid.Empty)
                tenantIds.Add(columnTenantId);

            var key = run.IdempotencyKey?.Trim();
            if (string.IsNullOrEmpty(key))
                continue;

            if (TryParseTenantIdFromIdempotencyKey(key, out var tenantId))
                tenantIds.Add(tenantId);
        }

        if (tenantIds.Count == 0)
            return new Dictionary<Guid, string>();

        return await db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => tenantIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Slug, cancellationToken);
    }

    [GeneratedRegex(@"^manual-tenant-(.+)-(\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ManualTenantIdempotencyKey();

    [GeneratedRegex(@"^import-tenant-(.+)-(\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ImportTenantIdempotencyKey();
}
