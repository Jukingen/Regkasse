namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Son geliştirme ortamı pg_dump/pg_restore ölçümü (startup’ta doldurulur; API istekleri için salt okunur).
/// </summary>
public interface IBackupPostgresClientToolingProbeState
{
    BackupPostgresClientToolingSnapshot Snapshot { get; }

    void SetSnapshot(BackupPostgresClientToolingSnapshot snapshot);
}
