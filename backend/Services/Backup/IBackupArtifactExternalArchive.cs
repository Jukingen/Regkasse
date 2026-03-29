using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Doğrulanmış staging artefaktlarını harici hedefe taşıyan sınır; kayıtlı uygulama <see cref="BackendDescriptor"/> ile yeteneklerini beyan eder.
/// </summary>
public interface IBackupArtifactExternalArchive
{
    /// <summary>Politika bayraklarından bağımsız, çalışan arka uç türü ve immutability sınırları.</summary>
    BackupExternalArchiveBackendDescriptor BackendDescriptor { get; }

    Task<BackupExternalArchiveOutcome> CopyStagingArtifactsAsync(
        Guid backupRunId,
        string stagingRootFull,
        string externalArchiveRootFull,
        IReadOnlyList<BackupArtifactDescriptor> artifacts,
        CancellationToken cancellationToken = default);
}
