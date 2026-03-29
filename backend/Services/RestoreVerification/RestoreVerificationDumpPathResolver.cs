using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services.RestoreVerification;

internal static class RestoreVerificationDumpPathResolver
{
    /// <summary>
    /// En yeni başarılı yedeklerden başlayarak aday sırasında ilk disk üzerinde bulunan mantıksal dump dosyasını seçer.
    /// </summary>
    public static async Task<(Guid backupRunId, string absolutePath, string relativeDescriptor)?> TryResolveAmongSucceededCandidatesAsync(
        AppDbContext db,
        BackupOptions backupOpts,
        IReadOnlyList<Guid> candidateSucceededRunIds,
        ILogger logger,
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

            var resolved = TryResolveDumpOnDisk(runId, artifact, backupOpts, logger, rank, candidateSucceededRunIds.Count);
            if (resolved != null)
                return (runId, resolved.Value.absolutePath, resolved.Value.relativeDescriptor);
        }

        logger.LogWarning(
            "Restore verification dump: no on-disk logical dump found after checking {CandidateCount} successful backup run(s) (staging then external archive per run).",
            candidateSucceededRunIds.Count);

        return null;
    }

    /// <summary>
    /// Tek çalıştırma için: önce staging, yoksa harici arşiv yolu; açık log mesajları operatör incelemesi içindir.
    /// </summary>
    private static (string absolutePath, string relativeDescriptor)? TryResolveDumpOnDisk(
        Guid runId,
        BackupArtifact artifact,
        BackupOptions backupOpts,
        ILogger logger,
        int candidateRank,
        int candidateTotal)
    {
        var descriptor = artifact.StorageDescriptor;
        if (string.IsNullOrWhiteSpace(descriptor))
        {
            logger.LogWarning(
                "Restore verification dump: candidate rank {Rank}/{Total}: backup run {BackupRunId} logical artifact has empty storage descriptor.",
                candidateRank,
                candidateTotal,
                runId);
            return null;
        }

        var stagingRoot = backupOpts.ArtifactStagingRoot;
        if (BackupArtifactPathResolver.TryResolveStagingAbsolute(stagingRoot, descriptor, out var stagingAbs))
        {
            if (File.Exists(stagingAbs))
            {
                logger.LogInformation(
                    "Restore verification dump: candidate rank {Rank}/{Total}: using staging file for backup run {BackupRunId} (descriptor {Descriptor}).",
                    candidateRank,
                    candidateTotal,
                    runId,
                    descriptor);
                return (stagingAbs, descriptor);
            }

            logger.LogInformation(
                "Restore verification dump: candidate rank {Rank}/{Total}: staging path resolved for backup run {BackupRunId} but file missing (descriptor {Descriptor}).",
                candidateRank,
                candidateTotal,
                runId,
                descriptor);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(stagingRoot))
            {
                logger.LogInformation(
                    "Restore verification dump: candidate rank {Rank}/{Total}: staging root not configured; skipped staging resolution for backup run {BackupRunId} (descriptor {Descriptor}).",
                    candidateRank,
                    candidateTotal,
                    runId,
                    descriptor);
            }
            else
            {
                logger.LogInformation(
                    "Restore verification dump: candidate rank {Rank}/{Total}: staging path could not be resolved under configured root for backup run {BackupRunId} (descriptor {Descriptor}).",
                    candidateRank,
                    candidateTotal,
                    runId,
                    descriptor);
            }
        }

        if (string.IsNullOrWhiteSpace(backupOpts.ExternalArchiveRoot))
        {
            logger.LogInformation(
                "Restore verification dump: candidate rank {Rank}/{Total}: external archive root not configured; cannot use archive path for backup run {BackupRunId}.",
                candidateRank,
                candidateTotal,
                runId);
            return null;
        }

        var ext = Path.GetFullPath(backupOpts.ExternalArchiveRoot.Trim());
        var name = Path.GetFileName(descriptor);
        if (string.IsNullOrEmpty(name))
        {
            logger.LogWarning(
                "Restore verification dump: candidate rank {Rank}/{Total}: cannot derive archive file name from descriptor for backup run {BackupRunId}.",
                candidateRank,
                candidateTotal,
                runId);
            return null;
        }

        var archived = Path.GetFullPath(Path.Combine(ext, runId.ToString("N"), name));
        if (File.Exists(archived))
        {
            logger.LogInformation(
                "Restore verification dump: candidate rank {Rank}/{Total}: using external archive file for backup run {BackupRunId} (descriptor {Descriptor}).",
                candidateRank,
                candidateTotal,
                runId,
                descriptor);
            return (archived, descriptor);
        }

        logger.LogInformation(
            "Restore verification dump: candidate rank {Rank}/{Total}: external archive file not found for backup run {BackupRunId} (descriptor {Descriptor}).",
            candidateRank,
            candidateTotal,
            runId,
            descriptor);

        return null;
    }
}
