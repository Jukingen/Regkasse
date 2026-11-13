namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Result of RKSV restore compliance evaluation or a queued validation restore request.
/// Does not imply a production restore ran.
/// </summary>
public sealed class RestoreResult
{
    public bool Succeeded { get; private init; }
    public string? Code { get; private init; }
    public string? Error { get; private init; }
    public Guid? BackupRunId { get; private init; }
    public Guid? RestoreRequestId { get; private init; }
    public string? TargetDatabaseName { get; private init; }
    public string? PreviewNote { get; private init; }

    public static RestoreResult Success() => new() { Succeeded = true };

    public static RestoreResult SuccessQueued(
        Guid backupRunId,
        Guid restoreRequestId,
        string targetDatabaseName) =>
        new()
        {
            Succeeded = true,
            BackupRunId = backupRunId,
            RestoreRequestId = restoreRequestId,
            TargetDatabaseName = targetDatabaseName,
            PreviewNote =
                "Validation-only restore request queued; pending second Super Admin approval. Production is not modified."
        };

    public static RestoreResult Fail(string code, string error) =>
        new()
        {
            Succeeded = false,
            Code = code,
            Error = error
        };
}
