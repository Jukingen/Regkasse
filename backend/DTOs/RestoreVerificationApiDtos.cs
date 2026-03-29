using System.ComponentModel.DataAnnotations;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.RestoreVerification;

namespace KasseAPI_Final.DTOs;

/// <summary>Restore worker config / dağıtık kilit sağlığı (HTTP tetiklemez).</summary>
public sealed class RestoreVerificationReadinessResponseDto
{
    public string Level { get; init; } = string.Empty;

    public IReadOnlyList<string> Issues { get; init; } = Array.Empty<string>();

    public bool WorkerEnabled { get; init; }

    public bool OrchestratorDistributedLockEnabled { get; init; }

    public string ScopeDisclaimer { get; init; } = string.Empty;
}

public static class RestoreVerificationReadinessMapper
{
    public static RestoreVerificationReadinessResponseDto ToDto(RestoreVerificationConfigurationHealthSnapshot snap) =>
        new()
        {
            Level = snap.Level.ToString(),
            Issues = snap.Issues,
            WorkerEnabled = snap.WorkerEnabled,
            OrchestratorDistributedLockEnabled = snap.OrchestratorDistributedLockEnabled,
            ScopeDisclaimer = snap.ScopeDisclaimer
        };
}

/// <summary>POST /trigger isteği gövdesi (tamamı isteğe bağlı).</summary>
public sealed class RestoreVerificationManualTriggerRequestDto
{
    [StringLength(200)]
    public string? IdempotencyKey { get; init; }
}

/// <summary>
/// POST /trigger yanıtı (JSON camelCase: <c>runId</c>, <c>orchestrationState</c>, <c>newQueuedRunCreated</c>, <c>existingRunReturned</c>, <c>run</c>).
/// </summary>
public sealed class RestoreVerificationTriggerResponseDto
{
    public Guid RunId { get; init; }

    /// <summary><see cref="RestoreVerificationTriggerOrchestrationState"/> — API’de sayısal enum (varsayılan JSON).</summary>
    public RestoreVerificationTriggerOrchestrationState OrchestrationState { get; init; }

    public bool NewQueuedRunCreated { get; init; }

    public bool ExistingRunReturned { get; init; }

    public required RestoreVerificationRunResponseDto Run { get; init; }
}

/// <summary>Restore drill result (dump inspection + optional isolated restore + optional fiscal SQL + optional live integrity). Not artifact checksum verification.</summary>
public sealed class RestoreVerificationRunResponseDto
{
    public Guid Id { get; init; }
    public RestoreVerificationStatus Status { get; init; }
    public RestoreVerificationTriggerSource TriggerSource { get; init; }
    public Guid? SourceBackupRunId { get; init; }

    /// <summary><c>pg_restore --list</c> / TOC okunabilirliği (checksum değil).</summary>
    public bool? DumpInspectionPassed { get; init; }

    public int? PgRestoreListExitCode { get; init; }
    public int? PgRestoreListLineCount { get; init; }

    /// <summary>İzole geçici DB’ye gerçek <c>pg_restore</c>.</summary>
    public bool RestoreAttemptExecuted { get; init; }

    public bool? RestoreAttemptPassed { get; init; }

    public int? RestoreAttemptExitCode { get; init; }

    public string? RestoreAttemptSkipReason { get; init; }

    /// <summary>Örn. <c>rv_v_…</c>; host/path yok.</summary>
    public string? RestoreTargetDbRedacted { get; init; }

    public bool FiscalSqlSkipped { get; init; }
    public string? FiscalSqlSkipReason { get; init; }
    public bool? FiscalSqlPassed { get; init; }
    public int? FiscalSqlFailCount { get; init; }
    public int? FiscalSqlWarnCount { get; init; }
    /// <summary>Canlı operasyonel DB üzerinde <see cref="KasseAPI_Final.Services.IIntegrityCheckService"/> (read-only); post-restore değil (clone bağlantısı hariç).</summary>
    public string? IntegrityScope { get; init; }

    public bool? IntegrityChecksPassed { get; init; }
    public DateTime RequestedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureDetail { get; init; }
    public string? RequestedByUserId { get; init; }
    public string? CorrelationId { get; init; }

    /// <summary>Manuel tetiklerde isteğe bağlı idempotency anahtarı.</summary>
    public string? IdempotencyKey { get; init; }

    /// <summary>Structured notes (outbox, TSE deferred, integrity interpretation).</summary>
    public string? DetailsJson { get; init; }

    /// <summary>Enqueue / run-start anındaki güvenli yapılandırma JSON özeti (null: eski satırlar).</summary>
    public string? ConfigSnapshotJson { get; init; }
}

public sealed class RestoreVerificationHistoryResponseDto
{
    public IReadOnlyList<RestoreVerificationRunResponseDto> Items { get; init; } = Array.Empty<RestoreVerificationRunResponseDto>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}

public static class RestoreVerificationRunMapper
{
    public static RestoreVerificationRunResponseDto ToDto(RestoreVerificationRun r) => new()
    {
        Id = r.Id,
        Status = r.Status,
        TriggerSource = r.TriggerSource,
        SourceBackupRunId = r.SourceBackupRunId,
        DumpInspectionPassed = r.PgRestoreListPassed,
        PgRestoreListExitCode = r.PgRestoreListExitCode,
        PgRestoreListLineCount = r.PgRestoreListLineCount,
        RestoreAttemptExecuted = r.RestoreAttemptExecuted,
        RestoreAttemptPassed = r.RestoreAttemptPassed,
        RestoreAttemptExitCode = r.RestoreAttemptExitCode,
        RestoreAttemptSkipReason = r.RestoreAttemptSkipReason,
        RestoreTargetDbRedacted = r.RestoreTargetDbRedacted,
        FiscalSqlSkipped = r.FiscalSqlSkipped,
        FiscalSqlSkipReason = r.FiscalSqlSkipReason,
        FiscalSqlPassed = r.FiscalSqlPassed,
        FiscalSqlFailCount = r.FiscalSqlFailCount,
        FiscalSqlWarnCount = r.FiscalSqlWarnCount,
        IntegrityScope = r.IntegrityScope,
        IntegrityChecksPassed = r.IntegrityChecksPassed,
        RequestedAt = r.RequestedAt,
        StartedAt = r.StartedAt,
        CompletedAt = r.CompletedAt,
        FailureCode = r.FailureCode,
        FailureDetail = r.FailureDetail,
        RequestedByUserId = r.RequestedByUserId,
        CorrelationId = r.CorrelationId,
        IdempotencyKey = r.IdempotencyKey,
        DetailsJson = r.DetailsJson,
        ConfigSnapshotJson = r.ConfigSnapshotJson
    };

    public static RestoreVerificationTriggerResponseDto ToTriggerResponseDto(RestoreVerificationManualTriggerResult r) =>
        new()
        {
            RunId = r.Run.Id,
            OrchestrationState = r.OrchestrationState,
            NewQueuedRunCreated = r.NewQueuedRunCreated,
            ExistingRunReturned = r.ExistingRunReturned,
            Run = ToDto(r.Run)
        };
}
