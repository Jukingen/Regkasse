using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services.RestoreVerification;

internal static class RestoreVerificationDumpPathResolver
{
    /// <summary>
    /// En yeni başarılı yedeklerden başlayarak aday sırasında ilk disk üzerinde bulunan mantıksal dump dosyasını seçer.
    /// </summary>
    public static async Task<(Guid backupRunId, Guid artifactId, string absolutePath, string relativeDescriptor)?> TryResolveAmongSucceededCandidatesAsync(
        AppDbContext db,
        BackupOptions backupOpts,
        IReadOnlyList<Guid> candidateSucceededRunIds,
        ILogger logger,
        IHostEnvironment? hostEnvironment,
        CancellationToken cancellationToken = default)
    {
        if (candidateSucceededRunIds.Count == 0)
        {
            logger.LogInformation(
                "Restore verification dump resolution: no successful backup runs to consider (empty candidate list).");
            return null;
        }

        logger.LogInformation(
            "Restore verification dump resolution: evaluating up to {CandidateCount} successful backup run(s), newest first.",
            candidateSucceededRunIds.Count);

        var artifactRows = await db.BackupArtifacts.AsNoTracking()
            .Where(a => candidateSucceededRunIds.Contains(a.BackupRunId) && a.ArtifactType == BackupArtifactType.LogicalDump)
            .ToListAsync(cancellationToken);

        var latestArtifactByRun = artifactRows
            .GroupBy(a => a.BackupRunId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.CreatedAt).First());

        var rank = 0;
        foreach (var runId in candidateSucceededRunIds)
        {
            rank++;
            if (!latestArtifactByRun.TryGetValue(runId, out var artifact))
            {
                logger.LogInformation(
                    "Restore verification dump: candidate rank {Rank}/{Total}: backup run {BackupRunId} has no logical dump artifact row.",
                    rank,
                    candidateSucceededRunIds.Count,
                    runId);
                continue;
            }

            var resolved = BackupArtifactOnDiskResolver.TryResolveStagingOrExternalArchive(
                runId,
                artifact,
                backupOpts,
                logger,
                hostEnvironment,
                rank,
                candidateSucceededRunIds.Count,
                "Restore verification dump");
            if (resolved != null)
                return (runId, artifact.Id, resolved.Value.absolutePath, resolved.Value.relativeDescriptor);
        }

        logger.LogWarning(
            "Restore verification dump: no on-disk logical dump found after checking {CandidateCount} successful backup run(s) (staging then external archive per run).",
            candidateSucceededRunIds.Count);

        return null;
    }
}
