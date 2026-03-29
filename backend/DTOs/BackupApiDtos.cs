using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.Backup;

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

    public IReadOnlyList<BackupArtifactResponseDto>? Artifacts { get; init; }
    public IReadOnlyList<BackupVerificationResponseDto>? Verifications { get; init; }
}

public sealed class BackupArtifactResponseDto
{
    public Guid Id { get; init; }
    public BackupArtifactType ArtifactType { get; init; }

    /// <summary>UI-safe staging locator (no host path).</summary>
    public string StorageLocator { get; init; } = string.Empty;

    public long? ByteSize { get; init; }
    public string? ContentHashSha256 { get; init; }
    public BackupArtifactLifecycleState LifecycleState { get; init; }

    /// <summary>Redacted external key after successful archive copy (e.g. archive/runId/file).</summary>
    public string? ExternalRedactedLocator { get; init; }
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

public sealed class BackupConfigurationHealthResponseDto
{
    /// <summary>Healthy, Degraded, or Unhealthy.</summary>
    public string Level { get; init; } = string.Empty;

    public IReadOnlyList<string> Issues { get; init; } = Array.Empty<string>();

    public string EffectiveAdapterKind { get; init; } = string.Empty;

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
            EffectiveAdapterKind = snapshot.EffectiveAdapterKind.ToString(),
            WorkerEnabled = snapshot.WorkerEnabled,
            RealPostgreSqlLogicalDumpConfigured = snapshot.RealPostgreSqlLogicalDumpConfigured,
            BackupExecutionReality = snapshot.BackupExecutionReality,
            NonRealBackupAdapterAcknowledgmentConfigurationKey = snapshot.NonRealBackupAdapterAcknowledgmentConfigurationKey,
            ReadinessNarrative = snapshot.ReadinessNarrative,
            ArtifactVerificationDisclaimer = snapshot.ArtifactVerificationDisclaimer,
            RetentionReadiness = RetentionFromSnapshot(snapshot.RetentionReadiness),
            ExternalArchiveReadiness = ExternalArchiveFromSnapshot(snapshot.ExternalArchiveReadiness)
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
    public static BackupRunResponseDto ToDto(
        BackupRun run,
        bool includeChildren = false,
        bool? duplicateExecutionPreventedOverride = null,
        BackupArtifactPipelinePolicySnapshot? pipelinePolicy = null,
        bool materializedChildren = false,
        int? automaticRetryMaxAttemptsBudget = null)
    {
        var policy = pipelinePolicy ?? BackupPipelineProjector.DefaultPolicyForProjection;
        var completenessRequired = BackupCompletenessSuccessPolicy.TryParseAdapterKind(run.AdapterKind, out var adapterKind)
            && BackupCompletenessSuccessPolicy.CompletenessRequiredForSucceededRun(adapterKind);
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
            Artifacts = includeChildren
                ? run.Artifacts.Select(a => new BackupArtifactResponseDto
                {
                    Id = a.Id,
                    ArtifactType = a.ArtifactType,
                    StorageLocator = BackupArtifactPublicFormatter.RedactedStagingLocator(
                        a.ArtifactType,
                        a.StorageDescriptor),
                    ByteSize = a.ByteSize,
                    ContentHashSha256 = a.ContentHashSha256,
                    LifecycleState = a.LifecycleState,
                    ExternalRedactedLocator = a.ExternalRedactedLocator
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
}
