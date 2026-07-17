namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Outcome of <see cref="IBackupService.CreateBackupAsync"/>.
/// Enqueue-only: dump / checksum / retention run on the backup worker.
/// </summary>
public sealed class BackupResult
{
    public bool Succeeded { get; private init; }
    public string? Code { get; private init; }
    public string? Error { get; private init; }
    public Guid? BackupRunId { get; private init; }
    public BackupManualTriggerResultKind? TriggerKind { get; private init; }

    public static BackupResult Success(Guid backupRunId, BackupManualTriggerResultKind kind) =>
        new()
        {
            Succeeded = true,
            BackupRunId = backupRunId,
            TriggerKind = kind
        };

    public static BackupResult Fail(string code, string error) =>
        new()
        {
            Succeeded = false,
            Code = code,
            Error = error
        };
}
