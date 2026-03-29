namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Başarılı yedek çalıştırması için artefakt dosyasının sunucu diskinden güvenli indirilmesi (path traversal yok).
/// </summary>
public interface IBackupArtifactDownloadService
{
    /// <summary>
    /// İndirilebilir dosya yolunu doğrular; yalnızca <c>Succeeded</c> çalıştırmalar ve DB’de kayıtlı artefaktlar.
    /// </summary>
    Task<BackupArtifactDownloadPrepareResult> PrepareDownloadAsync(
        Guid backupRunId,
        Guid artifactId,
        CancellationToken cancellationToken = default);
}
