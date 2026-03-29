namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Artifact verification: metadata SHA-256 and optional on-disk re-hash when descriptors request it.
/// This is not restore verification, not pg_verifybackup, and does not prove RPO/RTO.
/// Runs in worker scope only.
/// </summary>
public interface IBackupVerificationService
{
    Task<BackupVerificationOutcome> VerifyArtifactsAsync(
        Guid backupRunId,
        IReadOnlyList<BackupArtifactDescriptor> artifacts,
        CancellationToken cancellationToken = default);
}
