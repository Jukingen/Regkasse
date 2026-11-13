using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Groups the latest full Tenant package, later incrementals, and recent System dumps for PITR planning.
/// </summary>
public sealed class BackupChainService : IBackupChainService
{
    private readonly AppDbContext _db;
    private readonly IWalArchiveService _wal;

    public BackupChainService(AppDbContext db, IWalArchiveService wal)
    {
        _db = db;
        _wal = wal;
    }

    public async Task<BackupChainResponseDto> GetChainAsync(
        Guid? tenantId,
        BackupRunAccessScope scope,
        CancellationToken cancellationToken = default)
    {
        var effectiveTenant = scope.IsDeploymentWide ? tenantId : scope.CallerTenantId;
        var wal = _wal.GetStatus();

        var runs = await _db.BackupRuns.AsNoTracking()
            .Where(r => r.Status == BackupRunStatus.Succeeded && r.CompletedAt != null)
            .OrderByDescending(r => r.CompletedAt)
            .Take(400)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!scope.IsDeploymentWide)
        {
            runs = runs
                .Where(r => r.Strategy == BackupStrategyKind.Tenant
                            && r.TenantId == scope.CallerTenantId)
                .ToList();
        }
        else if (effectiveTenant.HasValue)
        {
            runs = runs
                .Where(r => r.Strategy == BackupStrategyKind.System
                            || r.TenantId == effectiveTenant.Value)
                .ToList();
        }

        var tenantSlugs = await LoadTenantSlugsAsync(runs, cancellationToken).ConfigureAwait(false);

        var tenantRuns = runs
            .Where(r => r.Strategy == BackupStrategyKind.Tenant
                        && (!effectiveTenant.HasValue || r.TenantId == effectiveTenant))
            .OrderBy(r => r.CompletedAt)
            .ToList();

        BackupRun? full = null;
        foreach (var run in tenantRuns)
        {
            if (!BackupIncrementalPackageMetadata.TryReadIncrementalSinceUtc(run.ConfigSnapshotJson, out _))
                full = run;
        }

        var incrementals = new List<BackupChainItemDto>();
        if (full != null)
        {
            foreach (var run in tenantRuns.Where(r => r.CompletedAt >= full.CompletedAt && r.Id != full.Id))
            {
                if (!BackupIncrementalPackageMetadata.TryReadIncrementalSinceUtc(run.ConfigSnapshotJson, out var since))
                    continue;
                incrementals.Add(ToItem(run, "incremental", tenantSlugs, since, full.Id, wal));
            }
        }

        var systemItems = runs
            .Where(r => r.Strategy == BackupStrategyKind.System)
            .OrderByDescending(r => r.CompletedAt)
            .Take(12)
            .Select(r => ToItem(r, "system", tenantSlugs, null, null, wal))
            .ToList();

        var fullItem = full == null
            ? null
            : ToItem(full, "full", tenantSlugs, null, null, wal);

        var available = fullItem != null || systemItems.Count > 0;
        return new BackupChainResponseDto
        {
            TenantIdFilter = effectiveTenant,
            FullBackup = fullItem,
            Incrementals = incrementals,
            SystemBackups = systemItems,
            WalCoverageStartUtc = wal.OldestFileUtc,
            WalCoverageEndUtc = wal.NewestFileUtc,
            WalFileCount = wal.FileCount,
            RestorePointAvailable = available,
            Message = available
                ? "Chain lists the latest full Tenant package, later incrementals, and recent System dumps. Isolated restore uses a System dump; tenant ZIP is not pg_restore input."
                : "No succeeded backups are visible in this scope."
        };
    }

    private async Task<IReadOnlyDictionary<Guid, string>> LoadTenantSlugsAsync(
        IReadOnlyList<BackupRun> runs,
        CancellationToken cancellationToken)
    {
        var ids = runs
            .Select(r => r.TenantId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, string>();

        return await _db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => ids.Contains(t.Id))
            .Select(t => new { t.Id, t.Slug })
            .ToDictionaryAsync(t => t.Id, t => t.Slug, cancellationToken)
            .ConfigureAwait(false);
    }

    private static BackupChainItemDto ToItem(
        BackupRun run,
        string kind,
        IReadOnlyDictionary<Guid, string> slugs,
        DateTime? sinceUtc,
        Guid? parentId,
        WalArchiveStatusDto wal)
    {
        var completed = run.CompletedAt;
        var covered = wal.Enabled
                      && completed.HasValue
                      && (!wal.OldestFileUtc.HasValue || wal.OldestFileUtc <= completed)
                      && (!wal.NewestFileUtc.HasValue || wal.NewestFileUtc >= completed);
        string? slug = null;
        if (run.TenantId.HasValue)
            slugs.TryGetValue(run.TenantId.Value, out slug);

        return new BackupChainItemDto
        {
            RunId = run.Id,
            PackageKind = kind,
            Strategy = run.Strategy.ToString(),
            TenantId = run.TenantId,
            TenantSlug = slug,
            CompletedAtUtc = completed,
            IncrementalSinceUtc = sinceUtc,
            ParentRunId = parentId,
            CoveredByWal = covered
        };
    }
}
