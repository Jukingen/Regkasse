using KasseAPI_Final.Models.RestoreVerification;

namespace KasseAPI_Final.DTOs;

/// <summary>
/// Tek bir kanıt kilometre taşı; frontend yorum çıkarmadan render edebilir.
/// </summary>
public sealed class RestoreProofMilestoneSnapshotDto
{
    public RestoreProofMilestoneKind Kind { get; init; }

    /// <summary><c>backup_run</c> | <c>backup_artifact</c> | <c>restore_verification_run</c></summary>
    public string EntityType { get; init; } = "";

    public Guid? Id { get; init; }

    /// <summary>Tercihen <c>CompletedAt</c>; yoksa başlangıç/oluşturma.</summary>
    public DateTime? AsOfUtc { get; init; }

    public RestoreVerificationStatus? DrillStatus { get; init; }

    public Guid? SourceBackupRunId { get; init; }

    public Guid? SourceBackupArtifactId { get; init; }
}

/// <summary>
/// Muhafazakâr uyarı kodları; <see cref="SchemaVersion"/> ile birlikte sürümlenir.
/// </summary>
public sealed class RestoreProofMilestonesSemanticsDto
{
    public string SchemaVersion { get; init; } = "1";

    /// <summary>Örn. <c>LATEST_DRILL_ATTEMPT_FAILED</c>, <c>NEWER_SUCCESS_WITHOUT_L4_CONTINUITY_PROOF</c>.</summary>
    public IReadOnlyList<string> WarningCodes { get; init; } = Array.Empty<string>();

    public string ConservativeNote { get; init; } =
        "Milestones are queried independently by predicate; a newer failed drill does not remove older succeeded rows from the database.";
}

/// <summary>
/// Yedek / artifact / restore drill kanıtı için &quot;son&quot; ve &quot;son bilinen iyi&quot; ayırımları.
/// </summary>
public sealed class RestoreProofMilestonesResponseDto
{
    public RestoreProofMilestoneSnapshotDto? LatestBackupRun { get; init; }

    public RestoreProofMilestoneSnapshotDto? LatestPgDumpSucceededBackupRun { get; init; }

    public RestoreProofMilestoneSnapshotDto? LatestPgDumpSucceededArtifact { get; init; }

    public RestoreProofMilestoneSnapshotDto? LatestRestoreDrillAttempt { get; init; }

    public RestoreProofMilestoneSnapshotDto? LatestRestoreDrillSucceeded { get; init; }

    public RestoreProofMilestoneSnapshotDto? LastKnownGoodL4ContinuityProven { get; init; }

    public RestoreProofMilestoneSnapshotDto? LastKnownGoodL5HttpSmokeProven { get; init; }

    public RestoreProofMilestoneSnapshotDto? LastKnownGoodL5aRestoredDbSmokeProven { get; init; }

    public RestoreProofMilestonesSemanticsDto Semantics { get; init; } = new();
}
