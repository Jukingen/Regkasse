using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Aggregates product RKSV restore-readiness for succeeded backups (hash + strategy gates).
/// Does not re-hash files on disk (that is <see cref="IComplianceCheckService"/> at restore time).
/// </summary>
public sealed class BackupComplianceStatusService : IBackupComplianceStatusService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public BackupComplianceStatusService(AppDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<BackupComplianceStatusResponseDto> GetAsync(
        BackupRunAccessScope? accessScope = null,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var windowStart = nowUtc.AddDays(-30);

        var runs = await AccessibleRuns(accessScope)
            .AsNoTracking()
            .Include(r => r.Artifacts)
            .Where(r => r.Status == BackupRunStatus.Succeeded && r.RequestedAt >= windowStart)
            .OrderByDescending(r => r.CompletedAt ?? r.RequestedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        var tenantIds = runs
            .Where(r => r.TenantId is Guid tid && tid != Guid.Empty)
            .Select(r => r.TenantId!.Value)
            .Distinct()
            .ToList();

        var tenantNames = tenantIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Tenants.AsNoTracking()
                .Where(t => tenantIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

        var items = new List<BackupComplianceListItemDto>(runs.Count);
        var compliant = 0;
        var nonCompliant = 0;

        foreach (var run in runs)
        {
            var (ok, reason) = EvaluateRestoreReadiness(run);
            if (ok) compliant++;
            else nonCompliant++;

            string? tenantName = null;
            if (run.TenantId is Guid tid && tenantNames.TryGetValue(tid, out var name))
                tenantName = name;

            items.Add(new BackupComplianceListItemDto
            {
                BackupRunId = run.Id,
                Date = run.CompletedAt ?? run.RequestedAt,
                TenantId = run.TenantId,
                TenantName = tenantName,
                Strategy = run.Strategy,
                Status = run.Status.ToString(),
                Compliant = ok,
                Reason = reason
            });
        }

        var restoreWindow = accessScope is { IsDeploymentWide: true }
            ? await _db.ManualRestoreRequests.AsNoTracking()
                .Where(r => r.RequestedAt >= windowStart)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Completed = g.Count(r => r.Status == ManualRestoreRequestStatus.Completed),
                    Failed = g.Count(r => r.Status == ManualRestoreRequestStatus.Failed)
                })
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return new BackupComplianceStatusResponseDto
        {
            Total = items.Count,
            Compliant = compliant,
            NonCompliant = nonCompliant,
            AllCompliant = items.Count > 0 && nonCompliant == 0,
            LastCheckUtc = nowUtc,
            RestoreRequestsTotal = restoreWindow?.Total ?? 0,
            RestoreRequestsCompleted = restoreWindow?.Completed ?? 0,
            RestoreRequestsFailed = restoreWindow?.Failed ?? 0,
            Backups = items
        };
    }

    /// <summary>
    /// Product gates: Succeeded + logical dump SHA-256 present.
    /// Tenant packages are integrity-ready (not pg_restore); System dumps are validation-restore eligible metadata.
    /// </summary>
    internal static (bool Ok, string Reason) EvaluateRestoreReadiness(BackupRun run)
    {
        if (run.Status != BackupRunStatus.Succeeded)
            return (false, "backup_not_succeeded");

        var dump = run.Artifacts
            .Where(a => a.ArtifactType == BackupArtifactType.LogicalDump)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefault();

        if (dump is null)
            return (false, "missing_logical_dump");

        if (string.IsNullOrWhiteSpace(dump.ContentHashSha256) || dump.ContentHashSha256.Length != 64)
            return (false, "missing_sha256");

        return run.Strategy == BackupStrategyKind.Tenant
            ? (true, "tenant_package_integrity_ok")
            : (true, "system_dump_hash_present");
    }

    private IQueryable<BackupRun> AccessibleRuns(BackupRunAccessScope? accessScope)
    {
        var q = _db.BackupRuns.AsNoTracking();
        return accessScope == null
            ? q
            : BackupRunAccessEvaluator.ApplyCallerAccessFilter(q, accessScope);
    }
}
