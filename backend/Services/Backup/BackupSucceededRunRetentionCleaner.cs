using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Deletes succeeded backup runs (and on-disk artifacts) that fall outside the active retention policy.
/// Default: flat strategy windows (Tenant ~30d / System ~90d).
/// When <see cref="BackupOptions.SmartRetentionEnabled"/>: GFS thinning via <see cref="ISmartRetentionService"/>.
/// </summary>
public static class BackupSucceededRunRetentionCleaner
{
    /// <summary>Stages EF removes + file deletes; caller must invoke <see cref="DbContext.SaveChangesAsync"/>.</summary>
    public static async Task<int> DeleteExpiredSucceededRunsAsync(
        AppDbContext db,
        BackupOptions backupOptions,
        IHostEnvironment hostEnvironment,
        ILogger logger,
        int retentionDays,
        CancellationToken cancellationToken = default,
        ISmartRetentionService? smartRetention = null)
    {
        // Legacy single-window callers: treat as System retention when strategy-aware path not used.
        return await DeleteExpiredSucceededRunsAsync(
            db,
            backupOptions,
            hostEnvironment,
            logger,
            tenantRetentionDays: Math.Min(retentionDays, BackupStrategyPolicy.TenantRetentionDays),
            systemRetentionDays: retentionDays,
            cancellationToken,
            smartRetention);
    }

    public static async Task<int> DeleteExpiredSucceededRunsAsync(
        AppDbContext db,
        BackupOptions backupOptions,
        IHostEnvironment hostEnvironment,
        ILogger logger,
        int tenantRetentionDays,
        int systemRetentionDays,
        CancellationToken cancellationToken = default,
        ISmartRetentionService? smartRetention = null)
    {
        List<BackupRun> candidates;
        string policyLabel;

        if (backupOptions.SmartRetentionEnabled)
        {
            var smart = smartRetention ?? new SmartRetentionService();
            var succeeded = await db.BackupRuns
                .Include(r => r.Artifacts)
                .Include(r => r.Verifications)
                .Where(r => r.Status == BackupRunStatus.Succeeded && r.CompletedAt != null)
                .OrderBy(r => r.CompletedAt)
                .ToListAsync(cancellationToken);

            if (succeeded.Count == 0)
                return 0;

            var deleteIds = new HashSet<Guid>(
                smart.SelectRunsToDelete(
                    succeeded
                        .Select(r => new BackupRetentionCandidate(r.Id, r.CompletedAt!.Value))
                        .ToList()));

            candidates = succeeded.Where(r => deleteIds.Contains(r.Id)).ToList();
            policyLabel = "smart-gfs";
            logger.LogInformation(
                "Backup retention (smart GFS): succeeded={Succeeded}, selectedForDelete={DeleteCount}",
                succeeded.Count,
                candidates.Count);
        }
        else
        {
            var tenantDays = BackupStrategyPolicy.ResolveRetentionCutoffDays(
                BackupStrategyKind.Tenant,
                tenantRetentionDays,
                systemRetentionDays);
            var systemDays = BackupStrategyPolicy.ResolveRetentionCutoffDays(
                BackupStrategyKind.System,
                tenantRetentionDays,
                systemRetentionDays);

            var tenantCutoff = DateTime.UtcNow.AddDays(-tenantDays);
            var systemCutoff = DateTime.UtcNow.AddDays(-systemDays);

            candidates = await db.BackupRuns
                .Include(r => r.Artifacts)
                .Include(r => r.Verifications)
                .Where(r => r.Status == BackupRunStatus.Succeeded && r.CompletedAt != null)
                .Where(r =>
                    (r.Strategy == BackupStrategyKind.Tenant && r.CompletedAt < tenantCutoff)
                    || (r.Strategy == BackupStrategyKind.System && r.CompletedAt < systemCutoff))
                .OrderBy(r => r.CompletedAt)
                .ToListAsync(cancellationToken);

            policyLabel = $"flat-tenant-{tenantDays}d/system-{systemDays}d";
        }

        if (candidates.Count == 0)
            return 0;

        var staged = 0;
        foreach (var run in candidates)
        {
            foreach (var artifact in run.Artifacts)
            {
                if (BackupArtifactOnDiskResolver.TryResolveForSingleRun(
                        run.Id,
                        artifact,
                        backupOptions,
                        logger,
                        hostEnvironment,
                        "backup_retention_deletion",
                        out var absolute)
                    && File.Exists(absolute))
                {
                    try
                    {
                        File.Delete(absolute);
                        logger.LogInformation(
                            "Backup retention deleted artifact file runId={RunId}, artifactId={ArtifactId}, path={Path}",
                            run.Id, artifact.Id, absolute);
                    }
                    catch (IOException ex)
                    {
                        logger.LogWarning(ex,
                            "Backup retention could not delete file runId={RunId}, path={Path}",
                            run.Id, absolute);
                    }
                }
            }

            db.BackupRuns.Remove(run);
            staged++;
            logger.LogInformation(
                "Backup retention staged removal of succeeded run: runId={RunId}, strategy={Strategy}, completedAt={Completed:o}, policy={Policy}",
                run.Id, run.Strategy, run.CompletedAt, policyLabel);
        }

        return staged;
    }
}
