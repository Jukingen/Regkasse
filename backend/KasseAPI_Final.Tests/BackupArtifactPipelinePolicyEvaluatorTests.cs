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
}
