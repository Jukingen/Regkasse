namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>Validation-only isolated pg_restore + SQL checks (never production DB).</summary>
public sealed class ValidationRestoreExecutionRequest
{
    public required Guid BackupRunId { get; init; }

    /// <summary>
    /// Isolated database name (must use <c>restore_validation_</c> prefix). When null, a name is generated.
    /// </summary>
    public string? TargetDatabaseName { get; init; }

    public bool ValidationOnly { get; init; } = true;
}
