using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Central tenant access rules for deployment-wide <see cref="BackupRun"/> rows
/// (canonical <see cref="BackupRun.TenantId"/> with idempotency-key legacy fallback).
/// </summary>
public static class BackupRunAccessEvaluator
{
    public static bool IsRunAccessible(BackupRun run, BackupRunAccessScope scope)
    {
        if (scope.IsDeploymentWide)
            return true;

        if (!scope.CallerTenantId.HasValue || scope.CallerTenantId.Value == Guid.Empty)
            return false;

        var callerTenantId = scope.CallerTenantId.Value;
        if (run.TenantId.HasValue)
            return run.TenantId.Value == callerTenantId;

        if (IsSharedDeploymentWideRun(run))
            return true;

        if (BackupRunTenantSlugResolver.MatchesTenantHint(run, callerTenantId))
            return true;

        return IsLegacyManualRunAccessibleToRequester(run, scope.CallerUserId);
    }

    public static IQueryable<BackupRun> ApplyCallerAccessFilter(
        IQueryable<BackupRun> query,
        BackupRunAccessScope scope)
    {
        if (scope.IsDeploymentWide)
            return query;

        if (!scope.CallerTenantId.HasValue || scope.CallerTenantId.Value == Guid.Empty)
            return query.Where(_ => false);

        var tenantId = scope.CallerTenantId.Value;
        var manualNeedle = $"manual-tenant-{tenantId}".ToLowerInvariant();
        var importNeedle = $"import-tenant-{tenantId}".ToLowerInvariant();
        var callerUserId = scope.CallerUserId;

        return query.Where(r =>
            r.TenantId == tenantId
            || (r.TenantId == null
                && (r.TriggerSource == BackupTriggerSource.Scheduled
                    || (r.IdempotencyKey != null
                        && r.IdempotencyKey.ToLower().StartsWith("manual-all-tenants"))))
            || (r.TenantId == null
                && r.IdempotencyKey != null
                && (r.IdempotencyKey.ToLower().Contains(manualNeedle)
                    || r.IdempotencyKey.ToLower().Contains(importNeedle)))
            || (r.TenantId == null
                && r.TriggerSource == BackupTriggerSource.Manual
                && callerUserId != null
                && r.RequestedByUserId == callerUserId
                && (r.IdempotencyKey == null
                    || (!r.IdempotencyKey.ToLower().Contains("manual-tenant-")
                        && !r.IdempotencyKey.ToLower().StartsWith("manual-all-tenants")
                        && !r.IdempotencyKey.ToLower().Contains("import-tenant-")))));
    }

    /// <summary>Tenant-scoped backup list/history filter (tenant_id column + shared deployment runs + legacy idempotency hint).</summary>
    public static IQueryable<BackupRun> ApplyTenantScopeFilter(IQueryable<BackupRun> query, Guid tenantId)
    {
        var manualNeedle = $"manual-tenant-{tenantId}".ToLowerInvariant();
        var importNeedle = $"import-tenant-{tenantId}".ToLowerInvariant();
        return query.Where(r =>
            r.TenantId == tenantId
            || (r.TenantId == null
                && (r.TriggerSource == BackupTriggerSource.Scheduled
                    || (r.IdempotencyKey != null
                        && r.IdempotencyKey.ToLower().StartsWith("manual-all-tenants"))))
            || (r.TenantId == null
                && r.IdempotencyKey != null
                && (r.IdempotencyKey.ToLower().Contains(manualNeedle)
                    || r.IdempotencyKey.ToLower().Contains(importNeedle))));
    }

    /// <summary>
    /// Deployment-wide backup rows readable/downloadable by any tenant-scoped Manager (shared PostgreSQL dump).
    /// </summary>
    public static bool IsSharedDeploymentWideRun(BackupRun run)
    {
        if (run.TenantId.HasValue)
            return false;

        if (run.TriggerSource == BackupTriggerSource.Scheduled)
            return true;

        var key = run.IdempotencyKey?.Trim();
        return !string.IsNullOrEmpty(key)
               && key.StartsWith("manual-all-tenants", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolves the manual-trigger scope used for duplicate-active-run detection.
    /// Non-null = tenant-bound enqueue; null = deployment-wide (Super Admin / all-tenants).
    /// </summary>
    public static Guid? ResolveManualTriggerScopeTenantId(Guid? ambientTenantId, string? idempotencyKey)
    {
        if (ambientTenantId is Guid ambient && ambient != Guid.Empty)
            return ambient;

        if (BackupRunTenantSlugResolver.TryParseTenantIdFromIdempotencyKey(idempotencyKey, out var parsed)
            && parsed != Guid.Empty)
        {
            return parsed;
        }

        return null;
    }

    /// <summary>
    /// Restricts duplicate-active-manual detection to runs in the same manual trigger scope.
    /// </summary>
    public static IQueryable<BackupRun> ApplyActiveManualConflictScope(
        IQueryable<BackupRun> query,
        Guid? manualScopeTenantId)
    {
        if (manualScopeTenantId.HasValue && manualScopeTenantId.Value != Guid.Empty)
        {
            var tenantId = manualScopeTenantId.Value;
            var manualNeedle = $"manual-tenant-{tenantId}".ToLowerInvariant();
            return query.Where(r =>
                r.TenantId == tenantId
                || (r.TenantId == null
                    && r.IdempotencyKey != null
                    && r.IdempotencyKey.ToLower().Contains(manualNeedle)));
        }

        return query.Where(r =>
            r.TenantId == null
            && (r.IdempotencyKey == null
                || r.IdempotencyKey.ToLower().StartsWith("manual-all-tenants")
                || (!r.IdempotencyKey.ToLower().Contains("manual-tenant-")
                    && !r.IdempotencyKey.ToLower().Contains("import-tenant-"))));
    }

    private static bool IsLegacyManualRunAccessibleToRequester(BackupRun run, string? callerUserId)
    {
        if (run.TriggerSource != BackupTriggerSource.Manual)
            return false;

        if (BackupRunTenantSlugResolver.EncodesTenantScope(run.IdempotencyKey))
            return false;

        return !string.IsNullOrWhiteSpace(callerUserId)
               && string.Equals(run.RequestedByUserId, callerUserId, StringComparison.Ordinal);
    }
}
