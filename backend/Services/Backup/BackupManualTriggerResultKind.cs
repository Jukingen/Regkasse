namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Result of a manual trigger request (HTTP layer). Distinct from <see cref="Models.Backup.BackupRunStatus"/>.
/// </summary>
public enum BackupManualTriggerResultKind
{
    /// <summary>New row created in Queued state.</summary>
    NewRunQueued = 0,

    /// <summary>Same idempotency key as an existing run; no new row.</summary>
    IdempotentReplay = 1,

    /// <summary>Another manual run is already active; no new row.</summary>
    DuplicateActiveManualPrevented = 2,
}
