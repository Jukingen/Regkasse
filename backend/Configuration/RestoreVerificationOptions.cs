namespace KasseAPI_Final.Configuration;

/// <summary>
/// Restore drill / restore confidence (pg_restore --list + optional fiscal SQL on isolated DB + live integrity read).
/// Does not replace artifact verification; does not perform destructive restore on production.
/// </summary>
public sealed class RestoreVerificationOptions
{
    public const string SectionName = "RestoreVerification";

    public bool WorkerEnabled { get; set; } = true;

    public TimeSpan OrchestratorPollingInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// True: worker tick başında <c>pg_try_advisory_lock</c> (Backup ile farklı anahtar çifti). Çoklu API örneğinde çift dequeue önlenir.
    /// </summary>
    public bool OrchestratorDistributedLockEnabled { get; set; } = true;

    /// <summary>Backup orchestrator anahtarlarından farklı olmalı (aynı PostgreSQL veritabanı).</summary>
    public int OrchestratorAdvisoryLockKey1 { get; set; } = unchecked((int)0x52676B74);

    /// <summary>İkinci advisory key (çiftin tamamı Backup ile çakışmamalı).</summary>
    public int OrchestratorAdvisoryLockKey2 { get; set; } = 1;

    /// <summary>When true, enqueue a scheduled drill when cadence is not satisfied (worker tick, advisory lock body).</summary>
    public bool ScheduledWeeklyDrillEnabled { get; set; }

    /// <summary>
    /// Zamanlanmış sıra için: son başarılı <em>Scheduled</em> drill kanıtının <c>CompletedAt</c> değerinden bu kadar gün
    /// geçmedikçe yeni Scheduled satırı eklenmez. Manuel başarılar cadence sayılmaz.
    /// </summary>
    public int ScheduledProofCadenceDays { get; set; } = 7;

    /// <summary>Optional connection string name for <c>fiscal_go_live_validation.sql</c>. Must not be DefaultConnection in Production.</summary>
    public string? FiscalValidationConnectionStringName { get; set; }

    /// <summary>Relative to content root (API project directory).</summary>
    public string FiscalValidationScriptRelativePath { get; set; } = Path.Combine("..", "scripts", "sql", "fiscal_go_live_validation.sql");

    public string? PgRestoreExecutablePath { get; set; }

    /// <summary>
    /// True ise geçici isimli DB oluşturulur, <c>pg_restore</c> uygulanır, ardından DB silinir. Prod uygulama <c>DefaultConnection</c> hedeflenmez.
    /// </summary>
    public bool IsolatedPgRestoreEnabled { get; set; }

    /// <summary>
    /// CREATEDB yetkili yönetim bağlantısı (çoğunlukla <c>Database=postgres</c>). Production’da <c>DefaultConnection</c> olamaz.
    /// </summary>
    public string? IsolatedRestoreAdminConnectionStringName { get; set; }

    /// <summary>İzole <c>pg_restore</c> için süre (saniye); en az 60.</summary>
    public int IsolatedPgRestoreTimeoutSeconds { get; set; } = 3600;

    /// <summary>Run <see cref="KasseAPI_Final.Services.IIntegrityCheckService"/> against the app operational DB (read-only); not post-restore unless DB is a restored clone.</summary>
    public bool IncludeLiveIntegrityChecks { get; set; } = true;

    public int IntegrityLookbackDays { get; set; } = 30;

    /// <summary>Restore drill worker lease süresi.</summary>
    public TimeSpan RunLeaseTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Lease / heartbeat yenileme aralığı.</summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Stale run reaper tarama aralığı.</summary>
    public TimeSpan StaleRecoveryScanInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Restore drill satırında lease kolonu null ise <see cref="RunLeaseTimeout"/> × çarpan sonrası reaper terminal yapar.
    /// </summary>
    public double StaleRecoveryNullLeaseGraceMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Restore drill dump seçimi: en yeni başarılı yedekten geriye doğru en fazla bu kadar çalıştırma disk üzerinde aranır (staging, sonra harici arşiv).
    /// </summary>
    public int DumpFallbackDepth { get; set; } = 5;

    /// <summary>
    /// True: Fake/ProductionStub gibi PgDump dışı başarılı yedekler de aday olabilir (geliştirme). False: yalnızca gerçek PostgreSQL pg_dump başarıları.
    /// </summary>
    public bool AllowNonPgDumpBackupSource { get; set; } = true;

    /// <summary>
    /// İzole <c>pg_restore</c> başarısından sonra geri yüklenen veritabanında şema ve süreklilik SQL kontrolleri.
    /// </summary>
    public bool PostRestoreSqlChecksEnabled { get; set; } = true;

    /// <summary>
    /// True: süreklilik SQL geçtikten sonra geri yüklenen izole DB üzerinde in-process uygulama dumanı (EF/migrasyon + salt okunur okuma).
    /// <see cref="PostRestoreSqlChecksEnabled"/> false iken anlamsız; doğrulayıcı reddeder.
    /// </summary>
    public bool RestoredDatabaseApplicationSmokeEnabled { get; set; }

    /// <summary>
    /// True: kurtarma kanıtına HTTP GET duman testi eklenir (<see cref="ApplicationSmokeProbeBaseUrl"/> ayrı dağıtım olabilir).
    /// </summary>
    public bool ApplicationSmokeProbeEnabled { get; set; }

    /// <summary>Mutlak http(s) taban URL (ör. yedek ortam API).</summary>
    public string? ApplicationSmokeProbeBaseUrl { get; set; }

    /// <summary>Taban URL’ye göre göreli yol (varsayılan <c>health</c>).</summary>
    public string ApplicationSmokeProbeRelativePath { get; set; } = "health";

    /// <summary>Duman testi zaman aşımı (saniye); en az 5.</summary>
    public int ApplicationSmokeProbeTimeoutSeconds { get; set; } = 30;
}
