namespace KasseAPI_Final.Configuration;

/// <summary>
/// Kalıcı admin seçimi: uygulama yapılandırmasındaki <see cref="BackupExecutionAdapterKind"/> ile birleşerek etkin yedek çalıştırma adaptörünü belirler.
/// </summary>
public enum AdminBackupRuntimeExecutionMode
{
    /// <summary>Yapılandırma dosyasındaki <c>Backup:ExecutionAdapterKind</c> kullanılır (varsayılan geriye dönük davranış).</summary>
    InheritFromConfiguration = 0,

    /// <summary>Her zaman <see cref="BackupExecutionAdapterKind.Fake"/> — simüle artefakt; PostgreSQL mantıksal yedek yok.</summary>
    SimulatedFake = 1,

    /// <summary>Her zaman <see cref="BackupExecutionAdapterKind.PgDump"/> — önkoşullar sağlanmazsa sağlık Unhealthy ve API seçimi reddedebilir.</summary>
    PostgreSqlPgDump = 2,
}
