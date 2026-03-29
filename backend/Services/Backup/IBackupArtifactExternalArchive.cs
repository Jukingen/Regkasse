using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Boundary for copying verified staging artifacts to an external target (filesystem in this implementation).
/// </summary>
public interface IBackupArtifactExternalArchive
{
    Task<BackupExternalArchiveOutcome> CopyStagingArtifactsAsync(
        Guid backupRunId,
        string stagingRootFull,
        string externalArchiveRootFull,
        IReadOnlyList<BackupArtifactDescriptor> artifacts,
        CancellationToken cancellationToken = default);
}
