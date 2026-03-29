namespace KasseAPI_Final.Services.Backup;

public interface IBackupManualTriggerService
{
    /// <summary>Enqueues a backup run. Does not execute backup work on the caller thread.</summary>
    Task<BackupManualTriggerOutcome> RequestManualBackupAsync(
        string? requestedByUserId,
        string requestedByRole,
        string? idempotencyKey,
        string? correlationId,
        CancellationToken cancellationToken = default);
}
