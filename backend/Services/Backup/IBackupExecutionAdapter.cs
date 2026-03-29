namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Boundary for PostgreSQL-native tools (pg_dump, pg_basebackup, WAL handling). Implementations run only from the worker/orchestrator, never from MVC request threads.
/// </summary>
public interface IBackupExecutionAdapter
{
    /// <summary>Stable label for persistence and ops (e.g. Fake, ProductionStub).</summary>
    string AdapterKind { get; }

    Task<BackupExecutionResult> ExecuteAsync(BackupExecutionContext context);
}
