namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Production adapter placeholder: does not invoke pg_dump/pg_basebackup yet. Fails closed so operators are not misled into thinking a real backup exists.
/// Phase 2+: implement using a dedicated process runner service (still not inside controllers).
/// </summary>
public sealed class PostgreSqlBackupExecutionAdapterStub : IBackupExecutionAdapter
{
    public string AdapterKind => "ProductionStub";

    public Task<BackupExecutionResult> ExecuteAsync(BackupExecutionContext context)
    {
        return Task.FromResult(new BackupExecutionResult
        {
            Success = false,
            ErrorCode = "ADAPTER_NOT_IMPLEMENTED",
            ErrorDetail =
                "ProductionStub performs no PostgreSQL I/O. For logical dumps set Backup:ExecutionAdapterKind=PgDump (Phase 2). Use Fake only in Development for simulation.",
            Artifacts = Array.Empty<BackupArtifactDescriptor>()
        });
    }
}
