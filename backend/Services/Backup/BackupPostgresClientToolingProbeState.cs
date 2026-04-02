namespace KasseAPI_Final.Services.Backup;

/// <summary>Thread-safe tek atımlık snapshot (startup sonrası sabit).</summary>
public sealed class BackupPostgresClientToolingProbeState : IBackupPostgresClientToolingProbeState
{
    private readonly object _gate = new();

    private BackupPostgresClientToolingSnapshot _snapshot = BackupPostgresClientToolingSnapshot.SkippedNotApplicable;

    public BackupPostgresClientToolingSnapshot Snapshot
    {
        get
        {
            lock (_gate)
                return _snapshot;
        }
    }

    public void SetSnapshot(BackupPostgresClientToolingSnapshot snapshot)
    {
        lock (_gate)
            _snapshot = snapshot;
    }
}
