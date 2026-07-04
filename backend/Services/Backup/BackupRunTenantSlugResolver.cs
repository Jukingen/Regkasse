using System.Text.RegularExpressions;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Resolves a filesystem-safe tenant slug label for backup artifact names from run metadata.
/// <see cref="BackupRun"/> has no <c>tenant_id</c>; manual triggers encode tenant in <see cref="BackupRun.IdempotencyKey"/>.
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

    /// <summary>Optional tenant scope via manual/import backup idempotency key (<see cref="BackupRun"/> has no tenant_id column).</summary>
    public static IQueryable<BackupRun> ApplyTenantHint(IQueryable<BackupRun> query, Guid? tenantId)
    {
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
            return query;

        var manualNeedle = $"manual-tenant-{tenantId.Value}".ToLowerInvariant();
        var importNeedle = $"import-tenant-{tenantId.Value}".ToLowerInvariant();
        return query.Where(r =>
            r.IdempotencyKey != null
            && (r.IdempotencyKey.ToLower().Contains(manualNeedle)
                || r.IdempotencyKey.ToLower().Contains(importNeedle)));
    }

    /// <summary>Whether <paramref name="run"/> belongs to <paramref name="tenantId"/> via idempotency key encoding.</summary>
    public static bool MatchesTenantHint(BackupRun run, Guid tenantId)
    {
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
