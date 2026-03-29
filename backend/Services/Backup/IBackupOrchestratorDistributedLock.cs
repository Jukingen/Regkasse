namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Çoklu API örneği için yedek orkestrasyonu tekilleştirir. HTTP katmanında kullanılmaz.
/// </summary>
public interface IBackupOrchestratorDistributedLock
{
    /// <summary>
    /// Kilit devre dışıysa: her zaman <see cref="BackupOrchestratorGateAttempt.DisabledBypass"/> ve no-op lease.
    /// Aksi halde: <see cref="BackupOrchestratorGateAttempt.AcquiredLease"/> ve serbest bırakılana kadar tutulan bağlantı,
    /// veya başka örnek tutuyorsa <see cref="BackupOrchestratorGateAttempt.ContendedElsewhere"/> (lease yok, kuyruk dokunulmaz).
    /// </summary>
    Task<(BackupOrchestratorGateAttempt Attempt, IAsyncDisposable? Lease)> TryEnterExclusiveAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Dağıtık kapı denemesi; sessiz başarı yok — çağıran lease yoksa yedek dequeue etmemelidir.
/// </summary>
public enum BackupOrchestratorGateAttempt
{
    /// <summary>Backup:OrchestratorDistributedLockEnabled=false — çoklu örnek riski bilinçli.</summary>
    DisabledBypass = 0,

    /// <summary>pg_try_advisory_lock başarılı; lease DisposeAsync ile unlock + bağlantı kapanır.</summary>
    AcquiredLease = 1,

    /// <summary>Başka oturum kilidi tutuyor; bu tick atlanır, Queued satırı değişmez.</summary>
    ContendedElsewhere = 2,

    /// <summary>DB bağlantısı / komut hatası; yedek çalıştırılmaz.</summary>
    ConnectionFailed = 3
}
