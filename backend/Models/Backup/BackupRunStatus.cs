namespace KasseAPI_Final.Models.Backup;

/// <summary>
/// Explicit backup run state machine. Terminal success is only reached after verification passes.
/// </summary>
public enum BackupRunStatus
{
    Queued = 0,
    Running = 1,
    AwaitingVerification = 2,
    /// <summary>Execution and verification succeeded.</summary>
    Succeeded = 3,
    Failed = 4,
    /// <summary>Execution finished but verification did not pass — distinct from Failed for ops alerting.</summary>
    VerificationFailed = 5,
    Cancelled = 6,
}
