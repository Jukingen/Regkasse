using System.Collections.Generic;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Resmi backup pipeline snapshot (computed). Restore verification drill akışından ayrıdır.
/// </summary>
public static class BackupPipelineProjector
{
    public const string ProjectionVersion = "2026-03-28";

    /// <summary>Ordered keys for every projected snapshot (including cancelled). Contract: frontend must match.</summary>
    public static readonly IReadOnlyList<string> PipelineStepKeysOrdered = new[]
    {
        "queued", "workerRunning", "dumpComplete", "artifactCreated",
        "artifactVerification", "manifestCreated", "externalCopy", "externalChecksum"
    };

    /// <summary>Legal values for <see cref="BackupPipelineStepDto.Status"/>.</summary>
    public static readonly IReadOnlySet<string> PipelineStepStatuses =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "pending", "running", "success", "failed", "skipped", "degraded", "not_required"
        };

    /// <summary>Policy bilinmiyorsa harici adımlar not_required (tetik yanıtı vb.).</summary>
    public static BackupArtifactPipelinePolicySnapshot DefaultPolicyForProjection { get; } = new()
    {
        ExternalArchiveRequirement = BackupExternalArchiveRequirementKind.NotApplicable,
        ExternalArchiveRootConfigured = false,
        ArtifactStagingRootConfigured = false,
        WillRunExternalArchiveAfterStagingVerificationWhenEligible = false,
        StagingOnDiskHashReverificationExpected = false,
        EffectiveAdapterKind = BackupExecutionAdapterKind.Fake,
        OperatorNotes = Array.Empty<string>()
    };

    public static BackupPipelineSnapshotDto Project(
        BackupRun run,
        BackupArtifactPipelinePolicySnapshot policy,
        bool materializedChildren)
    {
        if (run.Status == BackupRunStatus.Cancelled)
        {
            return new BackupPipelineSnapshotDto
            {
                OverallPhase = BackupPipelineOverallPhase.Cancelled,
                ProjectionVersion = ProjectionVersion,
                DataCompleteness = materializedChildren ? "full" : "partial_run_row_only",
                Steps = BuildCancelledSteps(run)
            };
        }

        var wantExternal = policy.WillRunExternalArchiveAfterStagingVerificationWhenEligible &&
                           policy.ExternalArchiveRootConfigured;

        BackupArtifact? logical = null;
        BackupArtifact? manifest = null;
        BackupVerification? primaryVerification = null;

        if (materializedChildren)
        {
            logical = run.Artifacts.FirstOrDefault(a => a.ArtifactType == BackupArtifactType.LogicalDump);
            manifest = run.Artifacts.FirstOrDefault(a => a.ArtifactType == BackupArtifactType.VerificationManifest);
            primaryVerification = PickPrimaryVerification(run);
        }

        var overallPhase = MapOverallPhase(run.Status);
        var completeness = materializedChildren ? "full" : "partial_run_row_only";

        var (copyStatus, copyMsg, copyCode) = StepExternalCopy(run, logical, wantExternal, materializedChildren);
        var (checksumStatus, checksumMsg, checksumCode) =
            StepExternalChecksum(run, logical, wantExternal, materializedChildren, copyStatus);

        var steps = new[]
        {
            Step("queued", StepQueued(run.Status), true, run.RequestedAt,
                run.Status > BackupRunStatus.Queued ? run.StartedAt : null, null, null),
            Step("workerRunning", StepWorker(run, logical, materializedChildren), true, run.StartedAt, null, null, null),
            Step("dumpComplete", StepDumpComplete(run, logical, materializedChildren), true, run.StartedAt, null, null, null),
            Step("artifactCreated", StepLogicalArtifact(run, logical, materializedChildren), true,
                logical?.CreatedAt, logical != null ? logical.CreatedAt : null, null, null),
            Step("artifactVerification", StepArtifactVerification(run, primaryVerification, materializedChildren), true,
                primaryVerification?.StartedAt, primaryVerification?.CompletedAt,
                primaryVerification?.Status == BackupVerificationStatus.Failed ? primaryVerification.FailureReason : null,
                primaryVerification?.Status == BackupVerificationStatus.Failed ? "VERIFICATION_FAILED" : null),
            Step("manifestCreated", StepManifest(run, manifest, logical, materializedChildren), true,
                manifest?.CreatedAt, manifest?.CreatedAt, null, null),
            Step("externalCopy", copyStatus, true, logical?.CreatedAt,
                ExternalStepCompletedAtUtc(run, copyStatus), copyMsg, copyCode),
            Step("externalChecksum", checksumStatus, true, logical?.CreatedAt,
                ExternalStepCompletedAtUtc(run, checksumStatus), checksumMsg, checksumCode),
        };

        return new BackupPipelineSnapshotDto
        {
            OverallPhase = overallPhase,
            ProjectionVersion = ProjectionVersion,
            DataCompleteness = completeness,
            Steps = steps
        };
    }

    private static IReadOnlyList<BackupPipelineStepDto> BuildCancelledSteps(BackupRun run) =>
        new[]
        {
            Step("queued", "success", true, run.RequestedAt, run.StartedAt, null, null),
            Step("workerRunning", "skipped", true, run.StartedAt, run.CompletedAt, "Run cancelled.", "CANCELLED"),
            Step("dumpComplete", "skipped", true, null, null, null, null),
            Step("artifactCreated", "skipped", true, null, null, null, null),
            Step("artifactVerification", "skipped", true, null, null, null, null),
            Step("manifestCreated", "skipped", true, null, null, null, null),
            Step("externalCopy", "skipped", true, null, null, null, null),
            Step("externalChecksum", "skipped", true, null, null, null, null),
        };

    private static BackupPipelineStepDto Step(
        string key,
        string status,
        bool applicable,
        DateTime? started,
        DateTime? completed,
        string? message,
        string? errorCode) =>
        new()
        {
            Key = key,
            Status = status,
            Applicable = applicable,
            StartedAtUtc = started,
            CompletedAtUtc = completed,
            Message = message,
            ErrorCode = errorCode
        };

    private static string MapOverallPhase(BackupRunStatus st) => st switch
    {
        BackupRunStatus.Queued => BackupPipelineOverallPhase.Queued,
        BackupRunStatus.Running => BackupPipelineOverallPhase.Running,
        BackupRunStatus.AwaitingVerification => BackupPipelineOverallPhase.AwaitingArtifactVerification,
        BackupRunStatus.Succeeded => BackupPipelineOverallPhase.Completed,
        BackupRunStatus.Failed => BackupPipelineOverallPhase.FailedExecution,
        BackupRunStatus.VerificationFailed => BackupPipelineOverallPhase.VerificationFailed,
        BackupRunStatus.Cancelled => BackupPipelineOverallPhase.Cancelled,
        _ => BackupPipelineOverallPhase.Unknown
    };

    private static string StepQueued(BackupRunStatus st) => st == BackupRunStatus.Queued ? "running" : "success";

    private static string StepWorker(BackupRun run, BackupArtifact? logical, bool materialized)
    {
        if (run.Status == BackupRunStatus.Queued) return "pending";
        if (run.Status == BackupRunStatus.Running) return "running";
        if (run.Status == BackupRunStatus.Failed && materialized && logical == null) return "failed";
        if (run.Status >= BackupRunStatus.AwaitingVerification || run.Status == BackupRunStatus.Succeeded ||
            run.Status == BackupRunStatus.VerificationFailed)
            return "success";
        return "pending";
    }

    private static string StepDumpComplete(BackupRun run, BackupArtifact? logical, bool materialized)
    {
        if (run.Status is BackupRunStatus.Queued or BackupRunStatus.Running) return "pending";
        if (run.Status == BackupRunStatus.Failed && materialized && logical == null) return "failed";
        if (!materialized && run.Status == BackupRunStatus.Failed) return "pending";
        if (run.Status >= BackupRunStatus.AwaitingVerification || run.Status == BackupRunStatus.Succeeded ||
            run.Status == BackupRunStatus.VerificationFailed)
            return "success";
        return "pending";
    }

    private static string StepLogicalArtifact(BackupRun run, BackupArtifact? logical, bool materialized)
    {
        if (run.Status is BackupRunStatus.Queued or BackupRunStatus.Running) return "pending";
        if (logical != null) return "success";
        if (!materialized) return "pending";
        if (run.Status == BackupRunStatus.Failed || run.Status == BackupRunStatus.VerificationFailed) return "failed";
        return "pending";
    }

    private static string StepArtifactVerification(
        BackupRun run,
        BackupVerification? pv,
        bool materialized)
    {
        if (run.Status is BackupRunStatus.Queued or BackupRunStatus.Running) return "pending";
        if (!materialized || pv == null)
            return run.Status == BackupRunStatus.AwaitingVerification ? "running" : "pending";

        return pv.Status switch
        {
            BackupVerificationStatus.Pending => run.Status == BackupRunStatus.AwaitingVerification ? "running" : "pending",
            BackupVerificationStatus.Passed => "success",
            BackupVerificationStatus.Failed => "failed",
            _ => "pending"
        };
    }

    private static string StepManifest(
        BackupRun run,
        BackupArtifact? manifest,
        BackupArtifact? logical,
        bool materialized)
    {
        if (run.Status is BackupRunStatus.Queued or BackupRunStatus.Running) return "pending";
        if (manifest != null) return "success";
        if (!materialized) return "pending";
        if (run.Status == BackupRunStatus.Failed || run.Status == BackupRunStatus.VerificationFailed) return "failed";
        if (run.Status == BackupRunStatus.Succeeded && logical != null) return "pending";
        return "pending";
    }

    private static (string Status, string? Message, string? ErrorCode) StepExternalCopy(
        BackupRun run,
        BackupArtifact? logical,
        bool wantExternal,
        bool materialized)
    {
        if (!wantExternal) return ("not_required", null, null);
        if (!materialized || logical == null)
            return ("pending", null, null);

        var ls = logical.LifecycleState;
        var stagingVerified = ls == BackupArtifactLifecycleState.StagingVerified;
        var externalOk = ls == BackupArtifactLifecycleState.ExternalCopyVerified;
        var externalBad = ls == BackupArtifactLifecycleState.ExternalCopyFailed;
        var terminalOk = run.Status == BackupRunStatus.Succeeded;
        var terminalVerifyFail = run.Status == BackupRunStatus.VerificationFailed;
        var externalNotRunThisRun = wantExternal && stagingVerified && terminalOk;

        if (externalBad) return ("degraded", "External archive copy failed.", "EXTERNAL_ARCHIVE_FAILED");
        if (externalOk) return ("success", null, null);
        if (externalNotRunThisRun)
            return ("skipped", "External archive not executed for this run (eligibility/policy).", null);
        if (run.Status is BackupRunStatus.Queued or BackupRunStatus.Running or BackupRunStatus.AwaitingVerification)
            return ("pending", null, null);
        if (stagingVerified && terminalVerifyFail)
            return ("degraded", "Staging verified but pipeline failed before or during external phase.", null);
        if (terminalOk || terminalVerifyFail) return ("pending", null, null);
        return ("pending", null, null);
    }

    private static (string Status, string? Message, string? ErrorCode) StepExternalChecksum(
        BackupRun run,
        BackupArtifact? logical,
        bool wantExternal,
        bool materialized,
        string copyStatus)
    {
        if (!wantExternal) return ("not_required", null, null);
        if (copyStatus is "not_required" or "skipped") return (copyStatus, null, null);
        if (!materialized || logical == null) return ("pending", null, null);

        var ls = logical.LifecycleState;
        var stagingVerified = ls == BackupArtifactLifecycleState.StagingVerified;
        var externalOk = ls == BackupArtifactLifecycleState.ExternalCopyVerified;
        var externalBad = ls == BackupArtifactLifecycleState.ExternalCopyFailed;
        var terminalOk = run.Status == BackupRunStatus.Succeeded;
        var terminalVerifyFail = run.Status == BackupRunStatus.VerificationFailed;
        var externalNotRunThisRun = wantExternal && stagingVerified && terminalOk;

        if (externalOk) return ("success", null, null);
        if (externalBad) return ("failed", "Post-copy checksum verification failed.", "EXTERNAL_ARCHIVE_FAILED");
        if (externalNotRunThisRun) return ("skipped", null, null);
        if (stagingVerified && terminalVerifyFail) return ("degraded", null, null);
        return ("pending", null, null);
    }

    private static DateTime? ExternalStepCompletedAtUtc(BackupRun run, string stepStatus)
    {
        if (stepStatus is not ("success" or "failed" or "degraded" or "skipped" or "not_required")) return null;
        return run.CompletedAt;
    }

    private static BackupVerification? PickPrimaryVerification(BackupRun run)
    {
        var list = run.Verifications.Where(v => v.BackupRunId == run.Id).ToList();
        if (list.Count == 0)
            list = run.Verifications.ToList();
        if (list.Count == 0) return null;
        return list
            .OrderByDescending(v => v.CompletedAt ?? DateTime.MinValue)
            .ThenByDescending(v => v.StartedAt)
            .First();
    }
}
