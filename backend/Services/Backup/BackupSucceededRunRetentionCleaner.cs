using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services.Backup;

/// <summary>Deletes succeeded backup runs (and on-disk artifacts) older than the configured retention window.</summary>
public static class BackupSucceededRunRetentionCleaner
{
    /// <summary>Stages EF removes + file deletes; caller must invoke <see cref="DbContext.SaveChangesAsync"/>.</summary>
    public static async Task<int> DeleteExpiredSucceededRunsAsync(
        AppDbContext db,
        BackupOptions backupOptions,
        IHostEnvironment hostEnvironment,
        ILogger logger,
        int retentionDays,
        CancellationToken cancellationToken = default)
    {
        if (retentionDays < 1)
            return 0;

        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var candidates = await db.BackupRuns
            .Include(r => r.Artifacts)
            .Include(r => r.Verifications)
            .Where(r => r.Status == BackupRunStatus.Succeeded
                        && r.CompletedAt != null
                        && r.CompletedAt < cutoff)
            .OrderBy(r => r.CompletedAt)
            .ToListAsync(cancellationToken);

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
                "Backup retention staged removal of succeeded run older than cutoff: runId={RunId}, completedAt={Completed:o}, retentionDays={Days}",
                run.Id, run.CompletedAt, retentionDays);
        }

        return staged;
    }
}
