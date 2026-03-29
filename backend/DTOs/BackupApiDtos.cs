using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;

namespace KasseAPI_Final.DTOs;

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
    public bool CompletenessFlag { get; init; }
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

    /// <summary>Engine/config readiness for admin dashboards (red when Unhealthy).</summary>
    public BackupConfigurationHealthResponseDto ConfigurationHealth { get; init; } = null!;

    /// <summary>Artifact staging / external copy beklentisi; <see cref="BackupConfigurationHealthResponseDto"/> ile birlikte yorumlanmalıdır.</summary>
    public BackupArtifactPipelinePolicyResponseDto ArtifactPipelinePolicy { get; init; } = null!;
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
}

public sealed class BackupConfigurationHealthResponseDto
{
    /// <summary>Healthy, Degraded, or Unhealthy.</summary>
    public string Level { get; init; } = string.Empty;

    public IReadOnlyList<string> Issues { get; init; } = Array.Empty<string>();

    public string EffectiveAdapterKind { get; init; } = string.Empty;

    public bool WorkerEnabled { get; init; }

    /// <summary>Explicitly states Phase 1 verification is not restore proof.</summary>
    public string ArtifactVerificationDisclaimer { get; init; } = string.Empty;
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
            OperatorNotes = snap.OperatorNotes
        };
}

public static class BackupRunMapper
{
    /// <param name="duplicateExecutionPreventedOverride">When set (e.g. manual trigger response), overrides DB flag for API clarity.</param>
    public static BackupRunResponseDto ToDto(
        BackupRun run,
        bool includeChildren = false,
        bool? duplicateExecutionPreventedOverride = null)
    {
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
                    FailureReason = v.FailureReason
                }).ToList()
                : null
        };
    }
}
