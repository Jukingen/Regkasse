using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.Backup;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupArtifactPipelinePolicyEvaluatorTests
{
    [Fact]
    public void ShouldRunExternalArchive_pgDump_non_dev_always_true_when_options_allow()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Staging);
        var opts = new BackupOptions
        {
            ExecutionAdapterKind = BackupExecutionAdapterKind.PgDump,
            ExternalArchiveRoot = @"D:\arc"
        };

        Assert.True(BackupArtifactPipelinePolicyEvaluator.ShouldRunExternalArchiveAfterStagingVerification(
            BackupExecutionAdapterKind.PgDump,
            env.Object,
            opts));
    }

    [Fact]
    public void ShouldRunExternalArchive_pgDump_dev_false_when_root_unset()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        var opts = new BackupOptions
        {
            ExecutionAdapterKind = BackupExecutionAdapterKind.PgDump,
            ExternalArchiveRoot = null
        };

        Assert.False(BackupArtifactPipelinePolicyEvaluator.ShouldRunExternalArchiveAfterStagingVerification(
            BackupExecutionAdapterKind.PgDump,
            env.Object,
            opts));
    }

    [Fact]
    public void ShouldRunExternalArchive_pgDump_dev_true_when_root_set()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        var opts = new BackupOptions
        {
            ExecutionAdapterKind = BackupExecutionAdapterKind.PgDump,
            ExternalArchiveRoot = "/tmp/ext"
        };

        Assert.True(BackupArtifactPipelinePolicyEvaluator.ShouldRunExternalArchiveAfterStagingVerification(
            BackupExecutionAdapterKind.PgDump,
            env.Object,
            opts));
    }

    [Fact]
    public void Evaluate_non_dev_pgDump_marks_external_required()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        var opts = new BackupOptions
        {
            ExecutionAdapterKind = BackupExecutionAdapterKind.PgDump,
            ExternalArchiveRoot = "/var/archive",
            VerifyLogicalDumpFileOnDisk = true,
            ArtifactStagingRoot = "/var/staging"
        };

        var snap = BackupArtifactPipelinePolicyEvaluator.Evaluate(opts, env.Object);
        Assert.Equal(BackupExternalArchiveRequirementKind.RequiredForProductionLike, snap.ExternalArchiveRequirement);
        Assert.True(snap.WillRunExternalArchiveAfterStagingVerificationWhenEligible);
        Assert.True(snap.StagingOnDiskHashReverificationExpected);
    }

    [Fact]
    public void Evaluate_adds_immutability_operator_note_when_required_and_not_acknowledged()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        var opts = new BackupOptions
        {
            ExecutionAdapterKind = BackupExecutionAdapterKind.PgDump,
            ExternalArchiveRoot = "/var/archive",
            VerifyLogicalDumpFileOnDisk = true,
            ArtifactStagingRoot = "/var/staging",
            RequireExternalArchiveImmutableTarget = true,
            ExternalArchiveImmutabilityAcknowledged = false
        };

        var snap = BackupArtifactPipelinePolicyEvaluator.Evaluate(opts, env.Object);
        Assert.Contains(snap.OperatorNotes, n => n.Contains("ImmutabilityAcknowledged", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_adds_retention_note_when_policy_enabled()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        var opts = new BackupOptions
        {
            ExecutionAdapterKind = BackupExecutionAdapterKind.Fake,
            RetentionPolicyMode = BackupRetentionPolicyMode.ReportOnly,
            ArtifactRetentionDays = 14
        };

        var snap = BackupArtifactPipelinePolicyEvaluator.Evaluate(opts, env.Object);
        Assert.Contains(snap.OperatorNotes, n => n.Contains("Retention policy", StringComparison.Ordinal));
        Assert.Contains(snap.OperatorNotes,
            n => n.Contains("executableStatus", StringComparison.Ordinal)
                 && n.Contains(BackupRetentionReadinessEvaluator.ExecutableStatusReportOnly, StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_adds_external_archive_disposition_note_when_missing_in_production()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        var opts = new BackupOptions
        {
            ExecutionAdapterKind = BackupExecutionAdapterKind.PgDump,
            ExternalArchiveRoot = "/var/archive",
            VerifyLogicalDumpFileOnDisk = true,
            ArtifactStagingRoot = "/var/staging",
            RequireExternalArchiveImmutableTarget = false,
            ExternalArchiveImmutabilityAcknowledged = false,
            ExternalArchiveMutableTargetAccepted = false
        };

        var snap = BackupArtifactPipelinePolicyEvaluator.Evaluate(opts, env.Object);
        Assert.Contains(snap.OperatorNotes, n => n.Contains("disposition", StringComparison.OrdinalIgnoreCase));
    }
}
