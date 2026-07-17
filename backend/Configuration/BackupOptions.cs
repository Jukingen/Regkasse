namespace KasseAPI_Final.Configuration;

/// <summary>
/// Phase 1: backup orchestration is config-driven; heavy execution stays out of HTTP and controllers.
/// Production pg_dump / pg_basebackup / WAL automation plugs in via <see cref="BackupExecutionAdapterKind"/> in later phases.
/// </summary>
/// <remarks>
/// Development’ta gerçek <c>pg_dump</c> (PgDump) kullanımı için yapılandırma: <c>docs/BACKUP_DEVELOPMENT_REAL_PG_DUMP.md</c>.
/// Kod Development ortamında PgDump’ı yasaklamaz; varsayılan <see cref="ExecutionAdapterKind"/> Fake seçilir.
/// </remarks>
public sealed class BackupOptions
{
    public const string SectionName = "Backup";

    /// <summary>When false, hosted orchestrator does not dequeue runs (manual inspection only).</summary>
    public bool WorkerEnabled { get; set; } = true;

    /// <summary>Polling interval for queued backup runs.</summary>
    public TimeSpan OrchestratorPollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// When true, worker opens a non-pooled PostgreSQL connection and takes <c>pg_try_advisory_lock</c> before dequeuing.
    /// Prevents two API instances from marking the same <see cref="BackupRunStatus.Queued"/> row as Running concurrently.
    /// </summary>
    public bool OrchestratorDistributedLockEnabled { get; set; } = true;

    /// <summary>Advisory lock first key (int4). Override if another subsystem uses the same pair in this database.</summary>
    public int OrchestratorAdvisoryLockKey1 { get; set; } = 0x52676B73; // "Rgks" ASCII — Regkasse backup orchestrator namespace

    /// <summary>Advisory lock second key (int4).</summary>
    public int OrchestratorAdvisoryLockKey2 { get; set; } = 1;

    /// <summary>Which adapter performs backup work (no shell in web layer — adapter runs inside worker scope).</summary>
    /// <remarks>
    /// <see cref="BackupExecutionAdapterKind.PgDump"/> için <see cref="ArtifactStagingRoot"/> ve geçerli bağlantı dizesi gerekir.
    /// Yerel geliştirme: docs/BACKUP_DEVELOPMENT_REAL_PG_DUMP.md.
    /// </remarks>
    public BackupExecutionAdapterKind ExecutionAdapterKind { get; set; } = BackupExecutionAdapterKind.Fake;

    /// <summary>Staging/root path for artifacts (adapter interprets; may be no-op for Fake).</summary>
    public string? ArtifactStagingRoot { get; set; }

    /// <summary>
    /// Legacy five-field UTC cron expression (CronFormat.Standard).
    /// Prefer <see cref="ScheduledBackupCron"/>; when <see cref="ScheduledBackupEnabled"/> is true, the effective expression is
    /// <see cref="ScheduledBackupCron"/> if set, otherwise this property.
    /// </summary>
    public string? ScheduleCronPlaceholder { get; set; }

    /// <summary>When true, the backup worker may enqueue scheduled backup runs on the UTC cron schedule (same gate as dequeue: <see cref="WorkerEnabled"/>).</summary>
    public bool ScheduledBackupEnabled { get; set; }

    /// <summary>UTC cron schedule (CronFormat.Standard). Falls back to <see cref="ScheduleCronPlaceholder"/> when null/whitespace.</summary>
    public string? ScheduledBackupCron { get; set; }

    /// <summary>Zamanlanmış yedek için etkin cron ifadesi (önce ScheduledBackupCron, yoksa ScheduleCronPlaceholder).</summary>
    public string? GetEffectiveScheduledBackupCronExpression()
    {
        if (!string.IsNullOrWhiteSpace(ScheduledBackupCron))
            return ScheduledBackupCron.Trim();
        if (!string.IsNullOrWhiteSpace(ScheduleCronPlaceholder))
            return ScheduleCronPlaceholder.Trim();
        return null;
    }

    /// <summary>Phase 2+: legacy placeholder; tercih edilen alan <see cref="ArtifactRetentionDays"/> + <see cref="RetentionPolicyMode"/>.</summary>
    public int? RetentionDaysPlaceholder { get; set; }

    /// <summary>
    /// DR hizası için saklama modu. <see cref="BackupRetentionPolicyMode.Disabled"/> dışında
    /// <see cref="ArtifactRetentionDays"/> zorunludur (7–3650 gün). Silme işi varsayılan kapalıdır.
    /// </summary>
    public BackupRetentionPolicyMode RetentionPolicyMode { get; set; } = BackupRetentionPolicyMode.Disabled;

    /// <summary>
    /// <see cref="RetentionPolicyMode"/> Disabled değilken zorunlu: artefakt için asgari saklama penceresi (gün).
    /// </summary>
    public int? ArtifactRetentionDays { get; set; }

    /// <summary>
    /// Development dışı + PgDump + dolu <see cref="ExternalArchiveRoot"/> iken:
    /// true ise operatör <see cref="ExternalArchiveImmutabilityAcknowledged"/> ile WORM/object-lock katmanını beyan etmeli;
    /// aksi halde BackupConfigurationEvaluation yapılandırmayı Unhealthy işaretler.
    /// false iken en az <see cref="ExternalArchiveImmutabilityAcknowledged"/> veya <see cref="ExternalArchiveMutableTargetAccepted"/>
    /// beklenir; aksi halde readiness Degraded olur (operatör beyanı zorunluluğu).
    /// </summary>
    public bool RequireExternalArchiveImmutableTarget { get; set; }

    /// <summary>
    /// Harici arşiv hedefinin (S3 Object Lock, WORM NAS vb.) operatör onayı.
    /// Kayıtlı harici arşiv arka ucu (şimdilik filesystem tabanlı) WORM/object-lock’u uygulama içinde doğrulamaz; admin readiness yanıtları beyan ile depolama gerçekliğini ayırır.
    /// </summary>
    public bool ExternalArchiveImmutabilityAcknowledged { get; set; }

    /// <summary>
    /// Production-benzeri ortamda PgDump + <see cref="ExternalArchiveRoot"/> iken, harici hedefin WORM/object-lock
    /// katmanı olmadığını operatör açıkça kabul eder. <see cref="RequireExternalArchiveImmutableTarget"/> false iken
    /// readiness, <see cref="ExternalArchiveImmutabilityAcknowledged"/> veya bu bayrak ile açık operatör beyanı ister.
    /// </summary>
    public bool ExternalArchiveMutableTargetAccepted { get; set; }

    /// <summary>
    /// Gelecekteki otomatik saklama silme işi için rezerve bayrak; şu an uygulama silme yapmaz ve doğrulama false dışında değeri reddeder.
    /// </summary>
    public bool RetentionArtifactDeletionEnabled { get; set; }

    /// <summary>When true, fake verifier fails (tests only).</summary>
    public bool DevelopmentForceVerificationFailure { get; set; }

    /// <summary>Phase 2+: alerting channel ids / webhook placeholders — publisher stays no-op except logs.</summary>
    public string? AlertingChannelPlaceholder { get; set; }

    /// <summary>
    /// Comma- or semicolon-separated ops recipients for German backup-failure emails
    /// (<c>EmailBackupAlertPublisher</c> / <c>IBackupFailureEmailAlertService</c>).
    /// Empty skips dedicated SMTP (activity-feed email path may still notify).
    /// Example: <c>admin@regkasse.at</c>.
    /// </summary>
    public string? FailureAlertEmailRecipients { get; set; }

    /// <summary>
    /// When true, staging artifacts are wrapped with AES-256-GCM after write
    /// (<see cref="Services.Backup.BackupEncryptionService"/>). Requires <see cref="EncryptionKeyBase64"/>.
    /// </summary>
    public bool EncryptionEnabled { get; set; }

    /// <summary>Base64-encoded 32-byte AES-256 key for artifact encryption at rest.</summary>
    public string? EncryptionKeyBase64 { get; set; }

    /// <summary>
    /// Production-like ortamda (Development dışı): <see cref="ExecutionAdapterKind"/> <see cref="BackupExecutionAdapterKind.ProductionStub"/> iken
    /// zorunlu. <c>pg_dump</c> / gerçek PostgreSQL mantıksal yedek çalıştırılmadığını operatör beyan eder.
    /// </summary>
    public bool AcknowledgePhase1NoRealBackup { get; set; }

    /// <summary>
    /// Production-like ortamda <see cref="ExecutionAdapterKind"/> <see cref="BackupExecutionAdapterKind.Fake"/> kullanımı için zorunlu açık onay.
    /// Sahte artefakt üretir; PostgreSQL yedekleme yoktur.
    /// </summary>
    public bool AcknowledgeFakeBackupAdapterOutsideDevelopment { get; set; }

    /// <summary>Path to pg_dump binary; default assumes PATH.</summary>
    public string? PgDumpExecutablePath { get; set; }

    /// <summary>Per-dump timeout (seconds); minimum 60 enforced by adapter.</summary>
    public int PgDumpTimeoutSeconds { get; set; } = 7200;

    /// <summary>
    /// Connection string name from configuration (e.g. DefaultConnection). Prefer a least-privilege backup role in production.
    /// </summary>
    public string? LogicalDumpConnectionStringName { get; set; }

    /// <summary>
    /// When true, verifier recomputes SHA-256 from on-disk files for logical dumps (artifact integrity, not restore proof).
    /// </summary>
    public bool VerifyLogicalDumpFileOnDisk { get; set; } = true;

    /// <summary>
    /// Secondary archive directory (absolute). Required in non-Development when <see cref="ExecutionAdapterKind"/> is <see cref="BackupExecutionAdapterKind.PgDump"/>; copies run after staging verification with post-copy SHA-256 check.
    /// </summary>
    public string? ExternalArchiveRoot { get; set; }

    /// <summary>Worker çalışırken lease süresi; heartbeat bu süreden önce yenilenmelidir.</summary>
    public TimeSpan RunLeaseTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Lease ve last_heartbeat alanlarını yenileme aralığı.</summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Stale run reaper tarama aralığı.</summary>
    public TimeSpan StaleRecoveryScanInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// <c>lease_expires_at_utc</c> null kalan eski veya bozuk satırlar: <see cref="RunLeaseTimeout"/> × bu çarpan kadar
    /// süre geçince reaper terminal yapar (canlı iş yükü için heartbeat’in lease yazması gerekir).
    /// </summary>
    public double StaleRecoveryNullLeaseGraceMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Otomatik requeue üst sınırı (başarısızlık başına planlanabilir tur sayısı; 0 = kapalı).
    /// Varsayılan 3 — gözetimsiz işletim için geçici hatalarda sınırlı yeniden deneme.
    /// </summary>
    public int AutomaticRetryMaxAttempts { get; set; } = 3;

    /// <summary>İlk yeniden deneme için taban gecikme; sonrakiler için üstel çarpan (üst sınır 24 saat).</summary>
    public TimeSpan AutomaticRetryInitialDelay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// false iken yalnızca <c>FailureCode=VERIFICATION_FAILED</c> (metadata/bütünlük) otomatik tekrarlanmaz.
    /// <c>VERIFICATION_WORKER_LOST</c> (stale reaper) bu bayrağa bağlı değildir — altyapı kaybı ayrı sınıftır.
    /// true iken requeue öncesi artefakt/doğrulama satırları silinir.
    /// </summary>
    public bool AllowAutomaticRetryAfterVerificationIntegrityFailure { get; set; }

    /// <summary>
    /// Operator-declared WAL archiving for PITR planning UI (not continuously verified by the worker).
    /// </summary>
    public bool PitrWalArchivingDeclaredEnabled { get; set; }

    /// <summary>
    /// Declared lag between last archived WAL and database "now" for PITR upper bound (minutes).
    /// </summary>
    public int? PitrWalArchiveDeclaredLagMinutes { get; set; }

    /// <summary>
    /// <c>pg_dump -Fc</c> compression level (<c>-Z</c>, 0–9). Default 6 balances size vs CPU (cost optimization).
    /// Custom format already uses zlib; this tunes the zlib level (not a separate .gz wrapper).
    /// </summary>
    public int PgDumpCompressionLevel { get; set; } = 6;

    /// <summary>
    /// Optional <c>--exclude-table</c> list for logical dumps (schema-qualified or bare names).
    /// Default excludes ASP.NET Identity credential tables so password hashes are not in dumps.
    /// Tenant business data, audit logs, and fiscal tables remain included. Empty array = dump all tables.
    /// </summary>
    public string[] LogicalDumpExcludeTables { get; set; } =
    {
        "AspNetUsers",
        "AspNetUserClaims",
        "AspNetUserLogins",
        "AspNetUserTokens",
    };

    /// <summary>
    /// When staging root disk usage reaches this percent, dashboard/health surfaces a cost/ops alert.
    /// Also used by <c>StorageAlertService</c> for periodic staging-volume alerts.
    /// </summary>
    public int StagingDiskUsageAlertPercent { get; set; } = 80;

    /// <summary>
    /// Poll interval for <c>StorageAlertService</c> (budget + staging disk). Minimum enforced: 5 minutes.
    /// </summary>
    public TimeSpan StorageAlertCheckInterval { get; set; } = TimeSpan.FromHours(6);

    /// <summary>
    /// When true, succeeded-run cleanup uses GFS smart retention
    /// (<see cref="Services.Backup.SmartRetentionService"/>: 7 daily / 4 weekly / 12 monthly / 7 yearly)
    /// instead of the flat Tenant 30d / System 90d cutoff.
    /// Default false preserves existing schedule retention windows.
    /// </summary>
    public bool SmartRetentionEnabled { get; set; }

    /// <summary>
    /// When true, post-success retention pass also reclassifies succeeded artifacts into
    /// Hot (≤7d) / Warm (≤30d) / Cold (&gt;30d) via <see cref="Services.Backup.StorageTierService"/>.
    /// Cold is a preference for external archive — not an automatic Glacier/S3 move.
    /// </summary>
    public bool StorageTierManagementEnabled { get; set; }

    /// <summary>Indicative EUR/GiB-month for Hot (fast staging). Ops estimate only.</summary>
    public decimal StorageCostHotEurPerGbMonth { get; set; } = 0.023m;

    /// <summary>Indicative EUR/GiB-month for Warm.</summary>
    public decimal StorageCostWarmEurPerGbMonth { get; set; } = 0.0125m;

    /// <summary>Indicative EUR/GiB-month for Cold (archive preference).</summary>
    public decimal StorageCostColdEurPerGbMonth { get; set; } = 0.004m;

    /// <summary>
    /// When true, <see cref="Services.Backup.AutomaticCleanupService"/> runs periodic retention cleanup
    /// (and optional storage-tier retag). Complements post-success cleanup.
    /// </summary>
    public bool AutomaticCleanupEnabled { get; set; }

    /// <summary>Interval between automatic cleanup passes. Minimum enforced: 1 hour. Default: 1 day.</summary>
    public TimeSpan AutomaticCleanupInterval { get; set; } = TimeSpan.FromDays(1);
}

/// <summary>Maps to registered <see cref="Services.Backup.IBackupExecutionAdapter"/> implementation.</summary>
public enum BackupExecutionAdapterKind
{
    Fake = 0,
    /// <summary>Throws / returns failed — safe default when real PostgreSQL tools are not wired.</summary>
    ProductionStub = 1,

    /// <summary>Logical custom-format dump via <c>pg_dump -Fc</c> (worker process only).</summary>
    PgDump = 2,
}
