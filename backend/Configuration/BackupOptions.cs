namespace KasseAPI_Final.Configuration;

/// <summary>
/// Phase 1: backup orchestration is config-driven; heavy execution stays out of HTTP and controllers.
/// Production pg_dump / pg_basebackup / WAL automation plugs in via <see cref="BackupExecutionAdapterKind"/> in later phases.
/// </summary>
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
    public BackupExecutionAdapterKind ExecutionAdapterKind { get; set; } = BackupExecutionAdapterKind.Fake;

    /// <summary>Staging/root path for artifacts (adapter interprets; may be no-op for Fake).</summary>
    public string? ArtifactStagingRoot { get; set; }

    /// <summary>Phase 2+: cron placeholder; not executed in Phase 1.</summary>
    public string? ScheduleCronPlaceholder { get; set; }

    /// <summary>Phase 2+: retention days placeholder.</summary>
    public int? RetentionDaysPlaceholder { get; set; }

    /// <summary>When true, fake verifier fails (tests only).</summary>
    public bool DevelopmentForceVerificationFailure { get; set; }

    /// <summary>Phase 2+: alerting channel ids / webhook placeholders — publisher stays no-op except logs.</summary>
    public string? AlertingChannelPlaceholder { get; set; }

    /// <summary>
    /// Non-Development only: when <see cref="ExecutionAdapterKind"/> is <see cref="BackupExecutionAdapterKind.ProductionStub"/>,
    /// must be true or startup validation fails. Documents operator acceptance that no real PostgreSQL backup runs yet.
    /// </summary>
    public bool AcknowledgePhase1NoRealBackup { get; set; }

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
