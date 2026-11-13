namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Outcome of <see cref="IBackupService.CreateBackupAsync"/>.
/// Enqueue-only: dump / checksum / retention run on the backup worker.
///
/// Named <c>BackupTriggerResult</c> rather than <c>BackupResult</c> because
/// <see cref="Billing.BackupResult"/> already owns that short name, and OpenAPI schema ids are built
/// from the short name — two types called <c>BackupResult</c> break spec generation outright.
/// </summary>
public sealed class BackupTriggerResult
{
    public bool Succeeded { get; private init; }
    public string? Code { get; private init; }
    public string? Error { get; private init; }
    public Guid? BackupRunId { get; private init; }
    public BackupManualTriggerResultKind? TriggerKind { get; private init; }

    public static BackupTriggerResult Success(Guid backupRunId, BackupManualTriggerResultKind kind) =>
        new()
        {
            Succeeded = true,
            BackupRunId = backupRunId,
            TriggerKind = kind
        };

    public static BackupTriggerResult Fail(string code, string error) =>
        new()
        {
            Succeeded = false,
            Code = code,
            Error = error
        };
}
