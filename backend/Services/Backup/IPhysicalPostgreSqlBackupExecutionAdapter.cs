namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Future boundary for physical cluster backup (e.g. <c>pg_basebackup</c>, replication slot / WAL policies).
/// Phase 2 implements only logical dump via <see cref="PostgreSqlPgDumpBackupExecutionAdapter"/>; no physical adapter is registered yet.
/// </summary>
public interface IPhysicalPostgreSqlBackupExecutionAdapter : IBackupExecutionAdapter
{
}
