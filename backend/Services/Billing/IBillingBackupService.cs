namespace KasseAPI_Final.Services.Billing;

public interface IBillingBackupService
{
    Task<BackupResult> BackupSaleAsync(
        Guid saleId,
        Guid? triggeredByUserId = null,
        CancellationToken ct = default);

    Task<BackupResult> BackupDailyAsync(
        DateTime date,
        Guid? triggeredByUserId = null,
        CancellationToken ct = default);

    Task<BackupResult> BackupWeeklyAsync(
        DateTime weekStart,
        Guid? triggeredByUserId = null,
        CancellationToken ct = default);

    Task<BackupResult> BackupFullAsync(
        Guid? triggeredByUserId = null,
        CancellationToken ct = default);

    Task<BackupHistoryListResponse> ListBackupHistoryAsync(
        BackupHistoryQuery query,
        CancellationToken ct = default);

    Task<BackupHistoryResponse> GetBackupDetailsAsync(
        Guid backupId,
        CancellationToken ct = default);

    Task<byte[]> DownloadBackupFileAsync(
        Guid backupId,
        CancellationToken ct = default);

    Task<int> CleanupExpiredBackupsAsync(
        CancellationToken ct = default);
}
