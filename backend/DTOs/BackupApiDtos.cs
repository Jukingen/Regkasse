using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.Backup;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.DTOs;

/// <summary>Computed backup pipeline overall phase (restore verification değildir).</summary>
public static class BackupPipelineOverallPhase
{
    public const string Unknown = "unknown";

    public const string Queued = "queued";

    public const string Running = "running";

    public const string AwaitingArtifactVerification = "awaiting_artifact_verification";

    public const string Completed = "completed";

    public const string FailedExecution = "failed_execution";

    public const string VerificationFailed = "verification_failed";

    public const string Cancelled = "cancelled";
}

public sealed class BackupPipelineStepDto
{
    public string Key { get; init; } = string.Empty;

    /// <summary>pending, running, success, failed, skipped, degraded, not_required</summary>
    public string Status { get; init; } = string.Empty;

    public bool Applicable { get; init; } = true;

    public DateTime? StartedAtUtc { get; init; }

    public DateTime? CompletedAtUtc { get; init; }

    public string? Message { get; init; }

    public string? ErrorCode { get; init; }
}

public sealed class BackupPipelineSnapshotDto
{
    public string OverallPhase { get; init; } = string.Empty;

    public string ProjectionVersion { get; init; } = string.Empty;

    /// <summary>full | partial_run_row_only (çocuk satırlar yüklenmediyse UI yanlış yeşil göstermesin).</summary>
    public string DataCompleteness { get; init; } = string.Empty;

    public IReadOnlyList<BackupPipelineStepDto> Steps { get; init; } = Array.Empty<BackupPipelineStepDto>();
}

/// <summary>İsteğe bağlı: admin detay uçlarında artefakt dosyası diskte mi (indirme ile aynı çözümleyici).</summary>
public sealed record BackupDownloadEnrichment(BackupOptions Options, IHostEnvironment HostEnvironment, ILogger Logger);

public sealed class BackupRunResponseDto
{
    public Guid Id { get; init; }
    public BackupRunStatus Status { get; init; }
    public BackupTriggerSource TriggerSource { get; init; }
    public string AdapterKind { get; init; } = string.Empty;
    public string? IdempotencyKey { get; init; }
    public string? RequestedByUserId { get; init; }
    public DateTime RequestedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureDetail { get; init; }
    public string? CorrelationId { get; init; }
    public bool DuplicatePrevented { get; init; }

    /// <summary>Otomatik requeue tur sayısı (başarılı otomatik yeniden kuyruğa alma sonrası artar).</summary>
    public int AutomaticRetryCount { get; init; }

    /// <summary>Planlanan otomatik requeue UTC zamanı; null ise bekleyen otomatik requeue yok.</summary>
    public DateTime? NextRetryAtUtc { get; init; }

    /// <summary>Son kayıtlı terminal hata kodu (Succeeded sonrası temizlenir).</summary>
    public string? LastRecordedTerminalFailureCode { get; init; }

    /// <summary>Bekleyen otomatik requeue sınıfı (İngilizce sabit; null: plan yok).</summary>
    public string? AutomaticRetryPendingClassifiedReason { get; init; }

    /// <summary>Son otomatik requeue planlamasının UTC zamanı.</summary>
    public DateTime? AutomaticRetryLastScheduledAtUtc { get; init; }

    /// <summary>Sunucudaki etkin <c>Backup:AutomaticRetryMaxAttempts</c> (bilgilendirme; 0 ise otomatik requeue kapalı).</summary>
    public int? AutomaticRetryMaxAttemptsBudget { get; init; }

    /// <summary>Enqueue / run-start anındaki güvenli yapılandırma JSON özeti (null: eski satırlar).</summary>
    public string? ConfigSnapshotJson { get; init; }

    /// <summary>
    /// İngilizce: <see cref="CompletenessFlag"/> ve terminal <c>Succeeded</c> ilişkisi (adapter’a göre; restore kanıtı değil).
    /// </summary>
    public string ArtifactCompletenessPolicyNote { get; init; } = string.Empty;

    /// <summary>Resmi pipeline; UI adımları buradan türetilmelidir.</summary>
    public BackupPipelineSnapshotDto Pipeline { get; init; } = null!;

    /// <summary>
    /// True when <see cref="AdapterKind"/> is Fake or ProductionStub (no pg_dump; stub or placeholder execution).
    /// </summary>
    public bool IsSimulatedExecution { get; init; }

    /// <summary>
    /// True when materialized artifacts include a LogicalDump row (metadata); does not assert production pg_dump format.
    /// </summary>
    public bool HasLogicalDumpArtifact { get; init; }

    /// <summary>Sum of artifact byte sizes when children are loaded; null when artifacts were not included.</summary>
    public long? TotalSizeBytes { get; init; }

    /// <summary>English human-readable total size (e.g. "12.5 MB").</summary>
    public string? TotalSizeFormatted { get; init; }

    /// <summary>Wall-clock duration from <see cref="StartedAt"/> to <see cref="CompletedAt"/> in seconds.</summary>
    public int? DurationSeconds { get; init; }

    /// <summary>English human-readable duration (e.g. "2m 30s").</summary>
    public string? DurationFormatted { get; init; }

    /// <summary>
    /// Backup artifact total size as percent of estimated original DB size when known;
    /// otherwise (original / logical dump) * 100 from artifact metadata.
    /// </summary>
    public double? CompressionRatio { get; init; }

    public IReadOnlyList<BackupArtifactResponseDto>? Artifacts { get; init; }
    public IReadOnlyList<BackupVerificationResponseDto>? Verifications { get; init; }
}

public sealed class ArtifactInfoDto
{
    public Guid Id { get; init; }
    public BackupArtifactType ArtifactType { get; init; }
    public long? ByteSize { get; init; }
    public string? FormattedSize { get; init; }
    public string StorageLocator { get; init; } = string.Empty;
    public DateTime? CreatedAt { get; init; }
}

public sealed class BackupArtifactResponseDto
{
    public Guid Id { get; init; }
    public BackupArtifactType ArtifactType { get; init; }

    /// <summary>UI-safe staging locator (no host path).</summary>
    public string StorageLocator { get; init; } = string.Empty;

    public long? ByteSize { get; init; }

    /// <summary>English human-readable size (e.g. "12.5 MB").</summary>
    public string? FormattedSize { get; init; }

    public DateTime? CreatedAt { get; init; }
    public string? ContentHashSha256 { get; init; }
    public BackupArtifactLifecycleState LifecycleState { get; init; }

    /// <summary>Redacted external key after successful archive copy (e.g. archive/runId/file).</summary>
    public string? ExternalRedactedLocator { get; init; }

    /// <summary>
    /// When <see cref="BackupDownloadEnrichment"/> was applied for this response: true if a file exists at the resolved path (same check as download).
    /// Otherwise false (unknown / not computed — e.g. history list without enrichment).
    /// </summary>
    public bool IsFilePresentForDownload { get; init; }
}

public sealed class BackupVerificationResponseDto
{
    public Guid Id { get; init; }
    public Guid BackupRunId { get; init; }
    public BackupVerificationStatus Status { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string VerifierSource { get; init; } = string.Empty;

    /// <summary>
    /// Doğrulanan listede mantıksal dump artefaktı var mı (restore doğrulaması değil).
    /// </summary>
    public bool CompletenessFlag { get; init; }

    /// <summary>
    /// Bu run adapter’ı için terminal başarı öncesi bu bayrağın zorunlu olup olmadığı (PgDump: true).
    /// </summary>
    public bool CompletenessRequiredForTerminalSuccess { get; init; }

    public string? FailureReason { get; init; }
}

public sealed class BackupTriggerRequestDto
{
    /// <summary>Optional idempotency key (reuse returns same run).</summary>
    public string? IdempotencyKey { get; init; }
}

/// <summary>Per-tenant backup automation knobs (<see cref="Models.Backup.BackupScheduleConfiguration"/>).</summary>
public sealed class BackupSettingsResponseDto
{
    public Guid TenantId { get; init; }

    public bool Enabled { get; init; }

    /// <summary>Five-field UTC cron (CronFormat.Standard).</summary>
    public string ScheduleCron { get; init; } = string.Empty;

    /// <summary>Best-effort parse of <see cref="ScheduleCron"/> for admin UI (daily/weekly/monthly/custom).</summary>
    public BackupScheduleConfigurationDto? Schedule { get; init; }

    public int RetentionDays { get; init; }

    public DateTime? LastRunAtUtc { get; init; }

    public DateTime? NextRunAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; init; }
}

public sealed class BackupSettingsPutRequestDto
{
    public bool Enabled { get; init; }

    /// <inheritdoc cref="BackupSettings.ScheduleCron"/>
    public string? ScheduleCron { get; init; }

    /// <summary>When set, takes precedence over <see cref="ScheduleCron"/>.</summary>
    public BackupScheduleConfigurationDto? Schedule { get; init; }

    /// <inheritdoc cref="BackupSettings.RetentionDays"/>
    public int RetentionDays { get; init; }
}

/// <summary>Latest scheduled backup row summary for scheduler UI.</summary>
public sealed class BackupScheduleLatestRunSummaryDto
{
    public Guid Id { get; init; }
    public BackupRunStatus Status { get; init; }
    public DateTime RequestedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureDetail { get; init; }
}

public sealed class BackupScheduleStatusResponseDto
{
    public bool DatabaseAutomationEnabled { get; init; }

    public string ScheduleCronUtc { get; init; } = string.Empty;

    public DateTime? StoredLastRunAtUtc { get; init; }

    public DateTime? StoredNextRunAtUtc { get; init; }

    /// <summary>Live projection from cron (may differ briefly from StoredNextRunAtUtc).</summary>
    public DateTime? ComputedNextRunAtUtc { get; init; }

    public BackupScheduleLatestRunSummaryDto? LatestScheduledBackupRun { get; init; }
}

public sealed class BackupTriggerResponseDto
{
    public BackupRunResponseDto Run { get; init; } = null!;

    /// <summary>True only when another manual run was already active (no new row).</summary>
    public bool DuplicateExecutionPrevented { get; init; }

    /// <summary>True only when a new backup run row was inserted in Queued state.</summary>
    public bool NewQueuedRunCreated { get; init; }

    /// <summary>
    /// Machine-readable orchestration state (not backup completion): e.g. NEW_RUN_QUEUED_AWAITING_WORKER.
    /// </summary>
    public string OrchestrationState { get; init; } = string.Empty;
}

public sealed class BackupHistoryResponseDto
{
    public IReadOnlyList<BackupRunResponseDto> Items { get; init; } = Array.Empty<BackupRunResponseDto>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}

public sealed class BackupLatestStatusResponseDto
{
    public BackupRunResponseDto? LatestRun { get; init; }
    public RestoreCapabilityDto Restore { get; init; } = null!;

    /// <summary>Engine/config readiness for admin dashboards (Unhealthy blocks unsafe posture; Degraded warns).</summary>
    public BackupConfigurationHealthResponseDto ConfigurationHealth { get; init; } = null!;

    /// <summary>Artifact staging / external copy beklentisi; <see cref="BackupConfigurationHealthResponseDto"/> ile birlikte yorumlanmalıdır.</summary>
    public BackupArtifactPipelinePolicyResponseDto ArtifactPipelinePolicy { get; init; } = null!;

    /// <summary>Son başarılı yedeklerin ortalama süresi (saniye); örnek yoksa null.</summary>
    public double? AverageSucceededBackupDurationSeconds { get; init; }

    /// <summary><see cref="AverageSucceededBackupDurationSeconds"/> için kullanılan başarılı çalıştırma sayısı.</summary>
    public int AverageSucceededBackupDurationSampleCount { get; init; }
}

/// <summary>
/// DR / recoverability proof özeti: en son yedek <em>istemi</em> ile son <em>başarılı kanıt</em> yüzeyleri ayrı.
/// REST: <c>GET /api/admin/backup/recoverability-summary</c> (camelCase JSON).
/// </summary>
public sealed class BackupRecoverabilitySummaryResponseDto
{
    /// <summary>Terminal başarılı yedek: <c>CompletedAt</c> (veya eksikse <c>RequestedAt</c>) sıralaması.</summary>
    public DateTime? LastSuccessfulBackupAt { get; init; }

    public Guid? LastSuccessfulBackupRunId { get; init; }

    /// <summary>
    /// Son başarılı yedek çalıştırması Fake/ProductionStub ise true; başarılı yedek yoksa null. Üretim pg_dump DR kanıtı sayılmaz.
    /// </summary>
    public bool? LastSuccessfulBackupRunIsSimulatedExecution { get; init; }

    /// <summary>Son geçen artifact (checksum/staging) doğrulaması zamanı.</summary>
    public DateTime? LastSuccessfulArtifactVerificationAt { get; init; }

    /// <summary>Zamanlanmış restore drill terminal başarısı <c>CompletedAt</c> (manuel tetiklenen başarılar dahil değil).</summary>
    public DateTime? LastSuccessfulRestoreProofAt { get; init; }

    public Guid? LastSuccessfulRestoreProofRunId { get; init; }

    /// <summary><see cref="LastSuccessfulBackupAt"/> yaşı; UTC şimdi − kanıt.</summary>
    public long? BackupProofAgeSeconds { get; init; }

    /// <summary><see cref="LastSuccessfulRestoreProofAt"/> yaşı (yalnızca zamanlanmış başarılı kanıt).</summary>
    public long? RestoreProofAgeSeconds { get; init; }

    /// <summary>En son kuyruğa alınan yedek isteği (<c>RequestedAt</c>).</summary>
    public DateTime? LatestRunAt { get; init; }

    public BackupRunStatus? LatestRunStatus { get; init; }

    /// <summary>En son restore verification isteği.</summary>
    public DateTime? LatestRestoreRunAt { get; init; }

    public RestoreVerificationStatus? LatestRestoreRunStatus { get; init; }

    /// <summary>Same values as latest status <see cref="BackupConfigurationHealthResponseDto.BackupExecutionReality"/> for DR dashboard context.</summary>
    public string BackupExecutionReality { get; init; } = string.Empty;

    /// <summary>True when worker is configured for real <c>pg_dump -Fc</c> logical backups.</summary>
    public bool RealPostgreSqlLogicalDumpConfigured { get; init; }

    /// <summary>Healthy / Degraded / Unhealthy — mirrors backup engine readiness.</summary>
    public string BackupReadinessLevel { get; init; } = string.Empty;

    /// <summary>English operator narrative (complements admin issues list).</summary>
    public string BackupReadinessNarrative { get; init; } = string.Empty;
}

/// <summary>Path içermez; yalnızca operatör özeti (restore verification değildir).</summary>
public sealed class BackupArtifactPipelinePolicyResponseDto
{
    public string ExternalArchiveRequirement { get; init; } = string.Empty;

    public bool ExternalArchiveRootConfigured { get; init; }

    public bool ArtifactStagingRootConfigured { get; init; }

    public bool WillRunExternalArchiveAfterStagingVerificationWhenEligible { get; init; }

    public bool StagingOnDiskHashReverificationExpected { get; init; }

    public string EffectiveAdapterKind { get; init; } = string.Empty;

    public IReadOnlyList<string> OperatorNotes { get; init; } = Array.Empty<string>();

    /// <summary>Örn. Filesystem — DI’daki <c>IBackupArtifactExternalArchive</c> ile uyumlu.</summary>
    public string RegisteredExternalArchiveBackendKind { get; init; } = string.Empty;

    /// <summary>NotEnforcedByApplication veya ApplicationEnforced (gelecek arka uç).</summary>
    public string ExternalArchiveImmutabilityEnforcement { get; init; } = string.Empty;

    public bool ApplicationEnforcesExternalArchiveImmutability { get; init; }

    /// <summary>İlk sınıf nesne depolama + immutability arka ucu bu sürümde yok (Filesystem dışı yol).</summary>
    public bool ObjectStorageImmutabilityBackendImplemented { get; init; }
}

public sealed class BackupRetentionReadinessResponseDto
{
    /// <summary>Disabled, ReportOnly, or ExecutionPlanned.</summary>
    public string Mode { get; init; } = string.Empty;

    public int? ArtifactRetentionDays { get; init; }

    public bool DeletionRequestedByConfiguration { get; init; }

    public bool AutomatedDeletionImplemented { get; init; }

    /// <summary>Örn. disabled, report_only_no_automated_enforcement, execution_planned_pending_implementation.</summary>
    public string ExecutableStatus { get; init; } = string.Empty;

    public IReadOnlyList<string> OperatorNotes { get; init; } = Array.Empty<string>();
}

public sealed class BackupConfigurationDiagnosticResponseDto
{
    /// <summary>Stable token, e.g. BACKUP_SETUP_DEV_ADAPTER_FAKE_NO_REAL_PG_DUMP.</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Information, Warning, or Error.</summary>
    public string Severity { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public IReadOnlyList<string>? RelatedConfigurationKeys { get; init; }
}

/// <summary>Admin seçim listesi: hangi kullanıcı modunun seçilebileceği ve engel nedeni.</summary>
public sealed class BackupExecutionSelectableModeDto
{
    /// <summary>Örn. UseConfigurationDefault, Fake, RealPgDump.</summary>
    public string UserFacingMode { get; init; } = string.Empty;

    /// <summary>PUT ile gönderilebilecek iç enum adı (geriye dönük).</summary>
    public string InternalMode { get; init; } = string.Empty;

    public bool Selectable { get; init; }

    /// <summary>Seçilemiyorsa İngilizce kısa gerekçe; aksi halde null.</summary>
    public string? BlockReason { get; init; }
}

public sealed class BackupExecutionModeResponseDto
{
    /// <summary>Kalıcı admin modu adı (InheritFromConfiguration, SimulatedFake, PostgreSqlPgDump).</summary>
    public string StoredMode { get; init; } = string.Empty;

    /// <summary>Kullanıcıya dönük: UseConfigurationDefault | Fake | RealPgDump.</summary>
    public string RequestedUserFacingMode { get; init; } = string.Empty;

    /// <summary>Yapılandırma dosyasındaki adaptörün kullanıcıya dönük karşılığı (Fake, RealPgDump, ProductionStub).</summary>
    public string ConfigurationDefaultUserFacingMode { get; init; } = string.Empty;

    /// <summary>Çözümlenmiş etkin adaptörün kullanıcıya dönük karşılığı.</summary>
    public string EffectiveUserFacingMode { get; init; } = string.Empty;

    /// <summary>
    /// Kalıcı RealPgDump + Unhealthy iken önerilen mod (UseConfigurationDefault). Çalışma zamanı adaptörü otomatik düşürülmez.
    /// </summary>
    public string? RecommendedFallbackUserFacingMode { get; init; }

    /// <summary>
    /// Yalnızca kalıcı mod Inherit olsaydı uygulanacak adaptör (bilgi amaçlı; geçersiz kılma yok).
    /// </summary>
    public string AdapterKindIfConfigurationDefaultOnly { get; init; } = string.Empty;

    /// <summary>Tek satır İngilizce çözüm özeti (günlük / operatör).</summary>
    public string EffectiveModeResolutionSummaryEnglish { get; init; } = string.Empty;

    public string ConfigurationExecutionAdapterKind { get; init; } = string.Empty;

    public string EffectiveExecutionAdapterKind { get; init; } = string.Empty;

    /// <summary>Etkin mod için yapılandırma sağlığı Unhealthy değilse true.</summary>
    public bool EffectiveModeRunnable { get; init; }

    /// <summary>
    /// PostgreSqlPgDump admin modu varsayıldığında sağlık (Real seçilebilirlik ve RealModeBlockingDiagnostics bununla uyumludur).
    /// </summary>
    public string HypotheticalPgDumpHealthLevel { get; init; } = string.Empty;

    /// <summary>Unhealthy ise engelleyici kısa mesajlar (İngilizce).</summary>
    public IReadOnlyList<string> Blockers { get; init; } = Array.Empty<string>();

    /// <summary>RealPgDump varsayılarak hesaplanan önkoşul tanıları (Error/Warning; pg_dump/pg_restore probları dahil).</summary>
    public IReadOnlyList<BackupConfigurationDiagnosticResponseDto> RealModeBlockingDiagnostics { get; init; } =
        Array.Empty<BackupConfigurationDiagnosticResponseDto>();

    /// <summary>Seçilebilir kullanıcı modları ve engel nedenleri.</summary>
    public IReadOnlyList<BackupExecutionSelectableModeDto> SelectableModes { get; init; } = Array.Empty<BackupExecutionSelectableModeDto>();

    /// <summary>Etkin adaptöre göre hesaplanan tam sağlık özeti.</summary>
    public BackupConfigurationHealthResponseDto EffectiveConfigurationHealth { get; init; } = null!;
}

public sealed class BackupExecutionModePutRequestDto
{
    /// <summary>UseConfigurationDefault (inherit), Fake, RealPgDump — veya iç enum adları / takma adlar (bkz. <c>BackupExecutionModeApiMapper.TryParseAdminMode</c>).</summary>
    public string Mode { get; init; } = string.Empty;

    /// <summary>Üretim benzeri ortamda SimulatedFake için zorunlu açık onay.</summary>
    public bool ConfirmSimulatedOnlyOperationalRiskInProduction { get; init; }
}

public sealed class BackupConfigurationHealthResponseDto
{
    /// <summary>Healthy, Degraded, or Unhealthy.</summary>
    public string Level { get; init; } = string.Empty;

    public IReadOnlyList<string> Issues { get; init; } = Array.Empty<string>();

    /// <summary>Machine-actionable setup reasons (merged with Development pg_dump/pg_restore probes when applicable).</summary>
    public IReadOnlyList<BackupConfigurationDiagnosticResponseDto> Diagnostics { get; init; } = Array.Empty<BackupConfigurationDiagnosticResponseDto>();

    public string EffectiveAdapterKind { get; init; } = string.Empty;

    /// <summary><c>appsettings</c> içindeki <c>Backup:ExecutionAdapterKind</c> (admin geçersiz kılmasından önce).</summary>
    public string ConfigurationExecutionAdapterKind { get; init; } = string.Empty;

    /// <summary>Kalıcı admin modu: InheritFromConfiguration, SimulatedFake, PostgreSqlPgDump.</summary>
    public string AdminRuntimeExecutionMode { get; init; } = string.Empty;

    public bool WorkerEnabled { get; init; }

    /// <summary>True when <c>ExecutionAdapterKind=PgDump</c>.</summary>
    public bool RealPostgreSqlLogicalDumpConfigured { get; init; }

    /// <summary>Stable token: PostgreSqlLogicalDump, SimulatedFake, ProductionStubNoPostgreSqlBackup, …</summary>
    public string BackupExecutionReality { get; init; } = string.Empty;

    /// <summary>Set when Fake or ProductionStub runs in a production-like environment with matching acknowledgment (configuration key for operators).</summary>
    public string? NonRealBackupAdapterAcknowledgmentConfigurationKey { get; init; }

    /// <summary>One-line English summary for dashboards.</summary>
    public string ReadinessNarrative { get; init; } = string.Empty;

    /// <summary>Explicitly states Phase 1 verification is not restore proof.</summary>
    public string ArtifactVerificationDisclaimer { get; init; } = string.Empty;

    /// <summary>Saklama politikasının yürütülebilirlik durumu; silme varsayılan kapalıdır.</summary>
    public BackupRetentionReadinessResponseDto RetentionReadiness { get; init; } = null!;

    /// <summary>Kayıtlı harici arşiv arka ucu ve immutability gerçekliği (yapılandırma onayından ayrı).</summary>
    public BackupExternalArchiveReadinessResponseDto ExternalArchiveReadiness { get; init; } = null!;
}

/// <summary>Harici arşiv: arka uç türü, zorlama düzeyi, sınırlamalar (İngilizce notlar).</summary>
public sealed class BackupExternalArchiveReadinessResponseDto
{
    public string RegisteredBackendKind { get; init; } = string.Empty;

    public string ImmutabilityEnforcement { get; init; } = string.Empty;

    public bool ApplicationEnforcesStorageImmutability { get; init; }

    public bool ObjectStorageImmutabilityBackendImplemented { get; init; }

    public IReadOnlyList<string> CapabilityOperatorNotes { get; init; } = Array.Empty<string>();
}

public static class BackupConfigurationHealthResponseMapper
{
    public static BackupConfigurationHealthResponseDto FromSnapshot(BackupConfigurationHealthSnapshot snapshot) =>
        new()
        {
            Level = snapshot.Level.ToString(),
            Issues = snapshot.Issues,
            Diagnostics = snapshot.Diagnostics.Select(FromDiagnostic).ToList(),
            EffectiveAdapterKind = snapshot.EffectiveAdapterKind.ToString(),
            ConfigurationExecutionAdapterKind = snapshot.ConfigurationExecutionAdapterKind.ToString(),
            AdminRuntimeExecutionMode = snapshot.AdminRuntimeExecutionMode.ToString(),
            WorkerEnabled = snapshot.WorkerEnabled,
            RealPostgreSqlLogicalDumpConfigured = snapshot.RealPostgreSqlLogicalDumpConfigured,
            BackupExecutionReality = snapshot.BackupExecutionReality,
            NonRealBackupAdapterAcknowledgmentConfigurationKey = snapshot.NonRealBackupAdapterAcknowledgmentConfigurationKey,
            ReadinessNarrative = snapshot.ReadinessNarrative,
            ArtifactVerificationDisclaimer = snapshot.ArtifactVerificationDisclaimer,
            RetentionReadiness = RetentionFromSnapshot(snapshot.RetentionReadiness),
            ExternalArchiveReadiness = ExternalArchiveFromSnapshot(snapshot.ExternalArchiveReadiness)
        };

    public static BackupConfigurationDiagnosticResponseDto FromDiagnostic(BackupConfigurationDiagnostic d) =>
        new()
        {
            Code = d.Code,
            Severity = d.Severity.ToString(),
            Message = d.Message,
            RelatedConfigurationKeys = d.RelatedConfigurationKeys
        };

    private static BackupExternalArchiveReadinessResponseDto ExternalArchiveFromSnapshot(
        BackupExternalArchiveReadinessSnapshot snap) =>
        new()
        {
            RegisteredBackendKind = snap.RegisteredBackendKind,
            ImmutabilityEnforcement = snap.ImmutabilityEnforcement.ToString(),
            ApplicationEnforcesStorageImmutability = snap.ApplicationEnforcesStorageImmutability,
            ObjectStorageImmutabilityBackendImplemented = snap.ObjectStorageImmutabilityBackendImplemented,
            CapabilityOperatorNotes = snap.CapabilityOperatorNotes
        };

    private static BackupRetentionReadinessResponseDto RetentionFromSnapshot(BackupRetentionReadinessSnapshot snap) =>
        new()
        {
            Mode = snap.Mode.ToString(),
            ArtifactRetentionDays = snap.ArtifactRetentionDays,
            DeletionRequestedByConfiguration = snap.DeletionRequestedByConfiguration,
            AutomatedDeletionImplemented = snap.AutomatedDeletionImplemented,
            ExecutableStatus = snap.ExecutableStatus,
            OperatorNotes = snap.OperatorNotes
        };
}

public sealed class RestoreCapabilityDto
{
    public bool IsAutomatedRestoreAvailable { get; init; }
    public string Notes { get; init; } = string.Empty;
}

public static class BackupArtifactPipelinePolicyMapper
{
    public static BackupArtifactPipelinePolicyResponseDto ToDto(BackupArtifactPipelinePolicySnapshot snap) =>
        new()
        {
            ExternalArchiveRequirement = snap.ExternalArchiveRequirement.ToString(),
            ExternalArchiveRootConfigured = snap.ExternalArchiveRootConfigured,
            ArtifactStagingRootConfigured = snap.ArtifactStagingRootConfigured,
            WillRunExternalArchiveAfterStagingVerificationWhenEligible =
                snap.WillRunExternalArchiveAfterStagingVerificationWhenEligible,
            StagingOnDiskHashReverificationExpected = snap.StagingOnDiskHashReverificationExpected,
            EffectiveAdapterKind = snap.EffectiveAdapterKind.ToString(),
            OperatorNotes = snap.OperatorNotes,
            RegisteredExternalArchiveBackendKind = snap.RegisteredExternalArchiveBackendKind,
            ExternalArchiveImmutabilityEnforcement = snap.ExternalArchiveImmutabilityEnforcement,
            ApplicationEnforcesExternalArchiveImmutability = snap.ApplicationEnforcesExternalArchiveImmutability,
            ObjectStorageImmutabilityBackendImplemented = snap.ObjectStorageImmutabilityBackendImplemented
        };
}

public static class BackupRunMapper
{
    /// <param name="duplicateExecutionPreventedOverride">When set (e.g. manual trigger response), overrides DB flag for API clarity.</param>
    /// <param name="materializedChildren">True when <see cref="BackupRun.Artifacts"/> / Verifications were loaded with the run (Include).</param>
    /// <param name="downloadEnrichment">When set with succeeded materialized run, populates <see cref="BackupArtifactResponseDto.IsFilePresentForDownload"/>.</param>
    /// <param name="estimatedOriginalDatabaseBytes">When set (e.g. pg_database_size estimate), overrides metadata-only compression ratio with backup-size percent of original.</param>
    public static BackupRunResponseDto ToDto(
        BackupRun run,
        bool includeChildren = false,
        bool? duplicateExecutionPreventedOverride = null,
        BackupArtifactPipelinePolicySnapshot? pipelinePolicy = null,
        bool materializedChildren = false,
        int? automaticRetryMaxAttemptsBudget = null,
        BackupDownloadEnrichment? downloadEnrichment = null,
        long? estimatedOriginalDatabaseBytes = null)
    {
        var policy = pipelinePolicy ?? BackupPipelineProjector.DefaultPolicyForProjection;
        var completenessRequired = BackupCompletenessSuccessPolicy.TryParseAdapterKind(run.AdapterKind, out var adapterKind)
            && BackupCompletenessSuccessPolicy.CompletenessRequiredForSucceededRun(adapterKind);
        var durationSeconds = BackupRunMetricsFormatter.ComputeDurationSeconds(run.StartedAt, run.CompletedAt);
        long? totalSizeBytes = null;
        double? compressionRatio = null;
        if (run.Artifacts.Count > 0)
        {
            totalSizeBytes = BackupRunMetricsFormatter.SumArtifactBytes(run.Artifacts);
            if (estimatedOriginalDatabaseBytes is > 0 && totalSizeBytes is > 0)
            {
                compressionRatio = BackupRunMetricsFormatter.ComputeBackupSizePercentOfOriginal(
                    totalSizeBytes.Value,
                    estimatedOriginalDatabaseBytes.Value);
            }
            else
            {
                compressionRatio = BackupRunMetricsFormatter.TryComputeCompressionRatio(run.Artifacts);
            }
        }

        return new BackupRunResponseDto
        {
            Id = run.Id,
            Status = run.Status,
            TriggerSource = run.TriggerSource,
            AdapterKind = run.AdapterKind,
            IdempotencyKey = run.IdempotencyKey,
            RequestedByUserId = run.RequestedByUserId,
            RequestedAt = run.RequestedAt,
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt,
            FailureCode = run.FailureCode,
            FailureDetail = run.FailureDetail,
            CorrelationId = run.CorrelationId,
            DuplicatePrevented = duplicateExecutionPreventedOverride ?? false,
            AutomaticRetryCount = run.AutomaticRetryCount,
            NextRetryAtUtc = run.NextRetryAtUtc,
            LastRecordedTerminalFailureCode = run.LastRecordedTerminalFailureCode,
            AutomaticRetryPendingClassifiedReason = run.AutomaticRetryPendingClassifiedReason,
            AutomaticRetryLastScheduledAtUtc = run.AutomaticRetryLastScheduledAtUtc,
            AutomaticRetryMaxAttemptsBudget = automaticRetryMaxAttemptsBudget,
            ConfigSnapshotJson = run.ConfigSnapshotJson,
            ArtifactCompletenessPolicyNote = BackupCompletenessSuccessPolicy.FormatCompletenessPolicyNote(run.AdapterKind),
            Pipeline = BackupPipelineProjector.Project(run, policy, materializedChildren),
            IsSimulatedExecution = ComputeIsSimulatedExecution(run.AdapterKind),
            HasLogicalDumpArtifact = ComputeHasLogicalDumpArtifact(includeChildren, run),
            TotalSizeBytes = totalSizeBytes,
            TotalSizeFormatted = BackupRunMetricsFormatter.FormatBytes(totalSizeBytes),
            DurationSeconds = durationSeconds,
            DurationFormatted = BackupRunMetricsFormatter.FormatDuration(durationSeconds),
            CompressionRatio = compressionRatio,
            Artifacts = includeChildren
                ? run.Artifacts.Select(a => new BackupArtifactResponseDto
                {
                    Id = a.Id,
                    ArtifactType = a.ArtifactType,
                    StorageLocator = BackupArtifactPublicFormatter.RedactedStagingLocator(
                        a.ArtifactType,
                        a.StorageDescriptor),
                    ByteSize = a.ByteSize,
                    FormattedSize = BackupRunMetricsFormatter.FormatBytes(a.ByteSize),
                    CreatedAt = a.CreatedAt,
                    ContentHashSha256 = a.ContentHashSha256,
                    LifecycleState = a.LifecycleState,
                    ExternalRedactedLocator = a.ExternalRedactedLocator,
                    IsFilePresentForDownload = ComputeArtifactFilePresentForDownload(
                        run,
                        a,
                        materializedChildren,
                        downloadEnrichment)
                }).ToList()
                : null,
            Verifications = includeChildren
                ? run.Verifications.Select(v => new BackupVerificationResponseDto
                {
                    Id = v.Id,
                    BackupRunId = v.BackupRunId,
                    Status = v.Status,
                    StartedAt = v.StartedAt,
                    CompletedAt = v.CompletedAt,
                    VerifierSource = v.VerifierSource,
                    CompletenessFlag = v.CompletenessFlag,
                    CompletenessRequiredForTerminalSuccess = completenessRequired,
                    FailureReason = v.FailureReason
                }).ToList()
                : null
        };
    }

    private static bool ComputeIsSimulatedExecution(string? adapterKind) =>
        BackupCompletenessSuccessPolicy.TryParseAdapterKind(adapterKind, out var k)
        && (k == BackupExecutionAdapterKind.Fake || k == BackupExecutionAdapterKind.ProductionStub);

    private static bool ComputeHasLogicalDumpArtifact(bool includeChildren, BackupRun run) =>
        includeChildren && run.Artifacts.Any(a => a.ArtifactType == BackupArtifactType.LogicalDump);

    private static bool ComputeArtifactFilePresentForDownload(
        BackupRun run,
        BackupArtifact artifact,
        bool materializedChildren,
        BackupDownloadEnrichment? enrichment)
    {
        if (!materializedChildren || enrichment == null || run.Status != BackupRunStatus.Succeeded)
            return false;

        return BackupArtifactOnDiskResolver.TryResolveForSingleRun(
            run.Id,
            artifact,
            enrichment.Options,
            enrichment.Logger,
            enrichment.HostEnvironment,
            "Backup run DTO: download availability",
            out _);
    }
}
