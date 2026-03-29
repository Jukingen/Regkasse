using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

public interface IBackupRunQueryService
{
    Task<BackupRun?> GetLatestRunAsync(CancellationToken cancellationToken = default);

    Task<BackupRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<BackupRun> Items, int TotalCount)> GetHistoryAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<BackupVerification?> GetLatestVerificationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// En yeni <see cref="BackupRunStatus.Succeeded"/> yedek çalıştırmalarının kimlikleri (RequestedAt azalan).
    /// </summary>
    Task<IReadOnlyList<Guid>> GetRecentSucceededRunIdsAsync(int maxCount, CancellationToken cancellationToken = default);
}
