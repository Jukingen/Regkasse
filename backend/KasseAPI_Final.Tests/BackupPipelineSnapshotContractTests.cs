using KasseAPI_Final.Configuration;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Snapshot shape invariants for admin DR UI / OpenAPI contract (not behavioral semantics).
/// </summary>
public sealed class BackupPipelineSnapshotContractTests
{
    private static readonly Guid RunId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static void AssertSnapshotContract(BackupPipelineSnapshotDto p)
    {
        Assert.False(string.IsNullOrWhiteSpace(p.ProjectionVersion));
        Assert.Equal(BackupPipelineProjector.ProjectionVersion, p.ProjectionVersion);
        Assert.NotNull(p.Steps);
        Assert.Equal(8, p.Steps.Count);
        Assert.Equal(BackupPipelineProjector.PipelineStepKeysOrdered, p.Steps.Select(s => s.Key).ToArray());
        foreach (var step in p.Steps)
        {
            Assert.True(
                BackupPipelineProjector.PipelineStepStatuses.Contains(step.Status),
                $"Unexpected status '{step.Status}' for step '{step.Key}'.");
        }

        Assert.Contains(p.DataCompleteness, new[] { "full", "partial_run_row_only" });
        Assert.False(string.IsNullOrWhiteSpace(p.OverallPhase));
    }

    [Fact]
    public void Cancelled_run_produces_contract_compliant_snapshot()
    {
        var run = new BackupRun
        {
            Id = RunId,
            Status = BackupRunStatus.Cancelled,
            RequestedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };

        var p = BackupPipelineProjector.Project(run, BackupPipelineProjector.DefaultPolicyForProjection, false);
        AssertSnapshotContract(p);
        Assert.Equal(BackupPipelineOverallPhase.Cancelled, p.OverallPhase);
    }

    [Fact]
    public void Partial_row_succeeded_produces_contract_compliant_snapshot()
    {
        var run = new BackupRun
        {
            Id = RunId,
            Status = BackupRunStatus.Succeeded,
            RequestedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };

        var p = BackupPipelineProjector.Project(run, BackupPipelineProjector.DefaultPolicyForProjection, false);
        AssertSnapshotContract(p);
    }

    [Fact]
    public void Queued_run_produces_contract_compliant_snapshot()
    {
        var run = new BackupRun
        {
            Id = RunId,
            Status = BackupRunStatus.Queued,
            RequestedAt = DateTime.UtcNow
        };

        var p = BackupPipelineProjector.Project(run, BackupPipelineProjector.DefaultPolicyForProjection, false);
        AssertSnapshotContract(p);
    }

    [Fact]
    public void Full_materialized_succeeded_produces_contract_compliant_snapshot()
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
                    LifecycleState = BackupArtifactLifecycleState.ExternalCopyVerified,
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

        var policy = new BackupArtifactPipelinePolicySnapshot
        {
            ExternalArchiveRequirement = BackupExternalArchiveRequirementKind.OptionalButConfigured,
            ExternalArchiveRootConfigured = true,
            ArtifactStagingRootConfigured = true,
            WillRunExternalArchiveAfterStagingVerificationWhenEligible = true,
            StagingOnDiskHashReverificationExpected = false,
            EffectiveAdapterKind = BackupExecutionAdapterKind.PgDump,
            OperatorNotes = Array.Empty<string>()
        };

        var p = BackupPipelineProjector.Project(run, policy, true);
        AssertSnapshotContract(p);
        Assert.Equal("full", p.DataCompleteness);
    }
}
