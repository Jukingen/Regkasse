using KasseAPI_Final.Configuration;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupPipelineProjectorTests
{
    private static readonly Guid RunId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private static BackupArtifactPipelinePolicySnapshot PolicyExternalOn() => new()
    {
        ExternalArchiveRequirement = BackupExternalArchiveRequirementKind.OptionalButConfigured,
        ExternalArchiveRootConfigured = true,
        ArtifactStagingRootConfigured = true,
        WillRunExternalArchiveAfterStagingVerificationWhenEligible = true,
        StagingOnDiskHashReverificationExpected = false,
        EffectiveAdapterKind = BackupExecutionAdapterKind.PgDump,
        OperatorNotes = Array.Empty<string>()
    };

    private static BackupArtifactPipelinePolicySnapshot PolicyExternalOff() => new()
    {
        ExternalArchiveRequirement = BackupExternalArchiveRequirementKind.NotApplicable,
        ExternalArchiveRootConfigured = false,
        ArtifactStagingRootConfigured = true,
        WillRunExternalArchiveAfterStagingVerificationWhenEligible = false,
        StagingOnDiskHashReverificationExpected = false,
        EffectiveAdapterKind = BackupExecutionAdapterKind.Fake,
        OperatorNotes = Array.Empty<string>()
    };

    [Fact]
    public void Partial_row_only_does_not_mark_artifact_success_for_succeeded_run()
    {
        var run = new BackupRun
        {
            Id = RunId,
            Status = BackupRunStatus.Succeeded,
            RequestedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };

        var p = BackupPipelineProjector.Project(run, PolicyExternalOn(), materializedChildren: false);

        Assert.Equal("partial_run_row_only", p.DataCompleteness);
        Assert.Equal(BackupPipelineOverallPhase.Completed, p.OverallPhase);
        Assert.Equal("pending", Status(p, "artifactCreated"));
        Assert.Equal("pending", Status(p, "externalCopy"));
    }

    [Fact]
    public void AwaitingVerification_without_verification_row_shows_running_not_false_pending()
    {
        var run = new BackupRun
        {
            Id = RunId,
            Status = BackupRunStatus.AwaitingVerification,
            RequestedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow,
            Artifacts =
            {
                new BackupArtifact
                {
                    BackupRunId = RunId,
                    ArtifactType = BackupArtifactType.LogicalDump,
                    LifecycleState = BackupArtifactLifecycleState.Staging
                }
            },
            Verifications = new List<BackupVerification>()
        };

        var p = BackupPipelineProjector.Project(run, PolicyExternalOff(), materializedChildren: true);

        Assert.Equal("running", Status(p, "artifactVerification"));
    }

    [Fact]
    public void Succeeded_staging_only_with_external_expected_marks_copy_skipped_and_degraded_checksum_semantics()
    {
        var run = new BackupRun
        {
            Id = RunId,
            Status = BackupRunStatus.Succeeded,
            RequestedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Artifacts =
            {
                new BackupArtifact
                {
                    BackupRunId = RunId,
                    ArtifactType = BackupArtifactType.LogicalDump,
                    LifecycleState = BackupArtifactLifecycleState.StagingVerified,
                    CreatedAt = DateTime.UtcNow
                },
                new BackupArtifact
                {
                    BackupRunId = RunId,
                    ArtifactType = BackupArtifactType.VerificationManifest,
                    LifecycleState = BackupArtifactLifecycleState.StagingVerified,
                    CreatedAt = DateTime.UtcNow
                }
            },
            Verifications =
            {
                new BackupVerification
                {
                    BackupRunId = RunId,
                    Status = BackupVerificationStatus.Passed,
                    StartedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow,
                    VerifierSource = "test"
                }
            }
        };

        var p = BackupPipelineProjector.Project(run, PolicyExternalOn(), materializedChildren: true);

        Assert.Equal("skipped", Status(p, "externalCopy"));
        Assert.Equal("skipped", Status(p, "externalChecksum"));
    }

    [Fact]
    public void External_copy_failed_is_degraded_copy_and_failed_checksum()
    {
        var run = new BackupRun
        {
            Id = RunId,
            Status = BackupRunStatus.VerificationFailed,
            RequestedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Artifacts =
            {
                new BackupArtifact
                {
                    BackupRunId = RunId,
                    ArtifactType = BackupArtifactType.LogicalDump,
                    LifecycleState = BackupArtifactLifecycleState.ExternalCopyFailed,
                    CreatedAt = DateTime.UtcNow
                }
            },
            Verifications =
            {
                new BackupVerification
                {
                    BackupRunId = RunId,
                    Status = BackupVerificationStatus.Failed,
                    StartedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow,
                    VerifierSource = "test",
                    FailureReason = "x"
                }
            }
        };

        var p = BackupPipelineProjector.Project(run, PolicyExternalOn(), materializedChildren: true);

        Assert.Equal("degraded", Status(p, "externalCopy"));
        Assert.Equal("failed", Status(p, "externalChecksum"));
    }

    [Fact]
    public void Cancelled_run_marks_worker_skipped()
    {
        var run = new BackupRun
        {
            Id = RunId,
            Status = BackupRunStatus.Cancelled,
            RequestedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };

        var p = BackupPipelineProjector.Project(run, PolicyExternalOn(), materializedChildren: false);

        Assert.Equal(BackupPipelineOverallPhase.Cancelled, p.OverallPhase);
        Assert.Equal("skipped", Status(p, "workerRunning"));
    }

    private static string Status(BackupPipelineSnapshotDto p, string key) =>
        p.Steps.Single(s => s.Key == key).Status;
}
