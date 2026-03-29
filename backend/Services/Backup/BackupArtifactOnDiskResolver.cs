using System.Diagnostics.CodeAnalysis;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models.Backup;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Yedek artefaktı için önce staging, yoksa harici arşiv yolunda okunabilir dosyayı çözer (restore drill ve güvenli indirme ortak).
/// </summary>
internal static class BackupArtifactOnDiskResolver
{
    /// <summary>
    /// Staging üzerinde dosya varsa onu; yoksa <c>ExternalArchiveRoot/{runId:N}/&lt;dosyaAdı&gt;</c> altındaki kopyayı döndürür.
    /// </summary>
    public static (string absolutePath, string relativeDescriptor)? TryResolveStagingOrExternalArchive(
        Guid runId,
        BackupArtifact artifact,
        BackupOptions backupOpts,
        ILogger logger,
        int candidateRank,
        int candidateTotal,
        string logScope)
    {
        var descriptor = artifact.StorageDescriptor;
        if (string.IsNullOrWhiteSpace(descriptor))
        {
            logger.LogWarning(
                "{LogScope}: candidate rank {Rank}/{Total}: backup run {BackupRunId} artifact has empty storage descriptor.",
                logScope,
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
                    "{LogScope}: candidate rank {Rank}/{Total}: using staging file for backup run {BackupRunId} (descriptor {Descriptor}).",
                    logScope,
                    candidateRank,
                    candidateTotal,
                    runId,
                    descriptor);
                return (stagingAbs, descriptor);
            }

            logger.LogInformation(
                "{LogScope}: candidate rank {Rank}/{Total}: staging path resolved for backup run {BackupRunId} but file missing (descriptor {Descriptor}).",
                logScope,
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
                    "{LogScope}: candidate rank {Rank}/{Total}: staging root not configured; skipped staging resolution for backup run {BackupRunId} (descriptor {Descriptor}).",
                    logScope,
                    candidateRank,
                    candidateTotal,
                    runId,
                    descriptor);
            }
            else
            {
                logger.LogInformation(
                    "{LogScope}: candidate rank {Rank}/{Total}: staging path could not be resolved under configured root for backup run {BackupRunId} (descriptor {Descriptor}).",
                    logScope,
                    candidateRank,
                    candidateTotal,
                    runId,
                    descriptor);
            }
        }

        if (string.IsNullOrWhiteSpace(backupOpts.ExternalArchiveRoot))
        {
            logger.LogInformation(
                "{LogScope}: candidate rank {Rank}/{Total}: external archive root not configured; cannot use archive path for backup run {BackupRunId}.",
                logScope,
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
                "{LogScope}: candidate rank {Rank}/{Total}: cannot derive archive file name from descriptor for backup run {BackupRunId}.",
                logScope,
                candidateRank,
                candidateTotal,
                runId);
            return null;
        }

        var archived = Path.GetFullPath(Path.Combine(ext, runId.ToString("N"), name));
        if (!BackupPathGuard.IsPathUnderRoot(archived, ext))
        {
            logger.LogWarning(
                "{LogScope}: candidate rank {Rank}/{Total}: resolved archive path left ExternalArchiveRoot for backup run {BackupRunId}.",
                logScope,
                candidateRank,
                candidateTotal,
                runId);
            return null;
        }

        if (File.Exists(archived))
        {
            logger.LogInformation(
                "{LogScope}: candidate rank {Rank}/{Total}: using external archive file for backup run {BackupRunId} (descriptor {Descriptor}).",
                logScope,
                candidateRank,
                candidateTotal,
                runId,
                descriptor);
            return (archived, descriptor);
        }

        logger.LogInformation(
            "{LogScope}: candidate rank {Rank}/{Total}: external archive file not found for backup run {BackupRunId} (descriptor {Descriptor}).",
            logScope,
            candidateRank,
            candidateTotal,
            runId,
            descriptor);

        return null;
    }

    /// <summary>
    /// Tek çalıştırma / indirme için log sırası olmadan çözüm (rank 1/1).
    /// </summary>
    public static bool TryResolveForSingleRun(
        Guid runId,
        BackupArtifact artifact,
        BackupOptions backupOpts,
        ILogger logger,
        string logScope,
        [NotNullWhen(true)] out string? absolutePath)
    {
        absolutePath = null;
        var r = TryResolveStagingOrExternalArchive(runId, artifact, backupOpts, logger, 1, 1, logScope);
        if (r == null)
            return false;
        absolutePath = r.Value.absolutePath;
        return true;
    }
}
