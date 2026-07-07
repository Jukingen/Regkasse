using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

public interface IBackupRunQueryService
{
    Task<BackupRun?> GetLatestRunAsync(
        BackupRunAccessScope? accessScope = null,
        CancellationToken cancellationToken = default);

    Task<BackupRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<BackupRun> Items, int TotalCount)> GetHistoryAsync(
        int page,
        int pageSize,
        BackupRunAccessScope? accessScope = null,
        CancellationToken cancellationToken = default);

    Task<BackupVerification?> GetLatestVerificationAsync(
        BackupRunAccessScope? accessScope = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// En yeni <see cref="BackupRunStatus.Succeeded"/> yedek çalıştırmalarının kimlikleri (RequestedAt azalan).
    /// </summary>
    Task<IReadOnlyList<Guid>> GetRecentSucceededRunIdsAsync(int maxCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gerçek PostgreSQL <c>pg_dump</c> yoluyla tamamlanmış başarılı yedekler (adaptör <c>PgDump</c>); restore drill için tercih edilen kaynak.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetRecentSucceededPgDumpRunIdsAsync(int maxCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Son tamamlanmış başarılı yedeklerin ortalama süresi (StartedAt → CompletedAt); UI tahmini için.
    /// </summary>
    Task<BackupSucceededDurationStatistics> GetAverageSucceededDurationAsync(
        int maxSamples,
        BackupRunAccessScope? accessScope = null,
        CancellationToken cancellationToken = default);
}
