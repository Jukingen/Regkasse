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

    /// <summary>When true, enqueue a drill if no successful scheduled run in the last 7 days (worker tick).</summary>
    public bool ScheduledWeeklyDrillEnabled { get; set; }

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
}
