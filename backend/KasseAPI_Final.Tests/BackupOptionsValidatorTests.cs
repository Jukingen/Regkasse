using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.Backup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupOptionsValidatorTests
{
    /// <summary>Builds configuration with optional ConnectionStrings entry (GetConnectionString is an extension — do not Moq it).</summary>
    private static IConfiguration ConfigWithConnectionString(string? connectionString, string connectionName = "DefaultConnection")
    {
        var dict = new Dictionary<string, string?>();
        if (connectionString != null)
            dict[$"ConnectionStrings:{connectionName}"] = connectionString;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public void Validate_Fake_in_Production_fails_without_explicit_acknowledgment()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var v = new BackupOptionsValidator(env.Object, ConfigWithConnectionString(null));
        var r = v.Validate(null, new BackupOptions { ExecutionAdapterKind = BackupExecutionAdapterKind.Fake });
        Assert.True(r.Failed);
        Assert.Contains("AcknowledgeFakeBackupAdapterOutsideDevelopment", r.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_Fake_in_Production_with_ack_passes_startup_but_evaluates_Degraded()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var v = new BackupOptionsValidator(env.Object, ConfigWithConnectionString(null));
        var r = v.Validate(null, new BackupOptions
        {
            ExecutionAdapterKind = BackupExecutionAdapterKind.Fake,
            AcknowledgeFakeBackupAdapterOutsideDevelopment = true
        });
        Assert.False(r.Failed);

        var snap = BackupConfigurationEvaluation.Evaluate(
            new BackupOptions
            {
                ExecutionAdapterKind = BackupExecutionAdapterKind.Fake,
                AcknowledgeFakeBackupAdapterOutsideDevelopment = true
            },
            env.Object);
        Assert.Equal(BackupConfigurationHealthLevel.Degraded, snap.Level);
        Assert.False(snap.RealPostgreSqlLogicalDumpConfigured);
        Assert.Equal(BackupConfigurationEvaluation.BackupExecutionRealitySimulatedFake, snap.BackupExecutionReality);
        Assert.Equal("Backup:AcknowledgeFakeBackupAdapterOutsideDevelopment", snap.NonRealBackupAdapterAcknowledgmentConfigurationKey);
    }

    [Fact]
    public void Validate_Fake_in_Staging_fails_without_ack()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Staging);

        var v = new BackupOptionsValidator(env.Object, ConfigWithConnectionString(null));
        var r = v.Validate(null, new BackupOptions { ExecutionAdapterKind = BackupExecutionAdapterKind.Fake });
        Assert.True(r.Failed);
    }

    [Fact]
    public void Validate_Fake_in_Development_succeeds()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        var v = new BackupOptionsValidator(env.Object, ConfigWithConnectionString(null));
        var r = v.Validate(null, new BackupOptions { ExecutionAdapterKind = BackupExecutionAdapterKind.Fake });
        Assert.False(r.Failed);
    }

    [Fact]
    public void Validate_ProductionStub_in_Production_without_ack_fails()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var v = new BackupOptionsValidator(env.Object, ConfigWithConnectionString(null));
        var r = v.Validate(null, new BackupOptions
        {
            ExecutionAdapterKind = BackupExecutionAdapterKind.ProductionStub,
            AcknowledgePhase1NoRealBackup = false
        });
        Assert.True(r.Failed);
        Assert.Contains("AcknowledgePhase1NoRealBackup", r.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("pg_dump", r.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ProductionStub_in_Production_with_ack_succeeds()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var v = new BackupOptionsValidator(env.Object, ConfigWithConnectionString(null));
        var r = v.Validate(null, new BackupOptions
        {
            ExecutionAdapterKind = BackupExecutionAdapterKind.ProductionStub,
            AcknowledgePhase1NoRealBackup = true
        });
        Assert.False(r.Failed);

        var snap = BackupConfigurationEvaluation.Evaluate(
            new BackupOptions
            {
                ExecutionAdapterKind = BackupExecutionAdapterKind.ProductionStub,
                AcknowledgePhase1NoRealBackup = true
            },
            env.Object);
        Assert.Equal(BackupConfigurationHealthLevel.Degraded, snap.Level);
        Assert.False(snap.RealPostgreSqlLogicalDumpConfigured);
        Assert.Equal(BackupConfigurationEvaluation.BackupExecutionRealityProductionStubNoPostgreSql, snap.BackupExecutionReality);
        Assert.Equal("Backup:AcknowledgePhase1NoRealBackup", snap.NonRealBackupAdapterAcknowledgmentConfigurationKey);
    }

    [Fact]
    public void Validate_PgDump_without_staging_root_fails_even_in_Development()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        var v = new BackupOptionsValidator(env.Object, ConfigWithConnectionString(null));
        var r = v.Validate(null, new BackupOptions
        {
            ExecutionAdapterKind = BackupExecutionAdapterKind.PgDump,
            ArtifactStagingRoot = null
        });
        Assert.True(r.Failed);
    }

    [Fact]
    public void Evaluate_WorkerDisabled_is_Degraded_not_Unhealthy()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        var snap = BackupConfigurationEvaluation.Evaluate(
            new BackupOptions { WorkerEnabled = false, ExecutionAdapterKind = BackupExecutionAdapterKind.Fake },
            env.Object);

        Assert.Equal(BackupConfigurationHealthLevel.Degraded, snap.Level);
    }

    [Fact]
    public void Evaluate_DistributedLockDisabled_in_Production_is_Degraded()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var snap = BackupConfigurationEvaluation.Evaluate(
            new BackupOptions
            {
                ExecutionAdapterKind = BackupExecutionAdapterKind.PgDump,
                ArtifactStagingRoot = OperatingSystem.IsWindows() ? @"C:\RegkasseBackup" : "/var/regkasse-backup",
                ExternalArchiveRoot = OperatingSystem.IsWindows() ? @"D:\RegkasseArchive" : "/var/regkasse-archive",
                VerifyLogicalDumpFileOnDisk = true,
                OrchestratorDistributedLockEnabled = false
            },
            env.Object,
            ConfigWithConnectionString("Host=h;Username=u;Password=p;Database=d"));

        Assert.Equal(BackupConfigurationHealthLevel.Degraded, snap.Level);
        Assert.Contains(
            snap.Issues,
            i => i.Contains("OrchestratorDistributedLockEnabled", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_PgDump_in_Production_without_on_disk_verify_fails()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var v = new BackupOptionsValidator(
            env.Object,
            ConfigWithConnectionString("Host=h;Username=u;Password=p;Database=d"));

        var r = v.Validate(null, new BackupOptions
        {
            ExecutionAdapterKind = BackupExecutionAdapterKind.PgDump,
            ArtifactStagingRoot = OperatingSystem.IsWindows() ? @"C:\RegkasseBackup" : "/var/regkasse-backup",
            VerifyLogicalDumpFileOnDisk = false
        });

        Assert.True(r.Failed);
    }

    [Fact]
    public void Validate_PgDump_in_Production_relative_staging_fails()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var v = new BackupOptionsValidator(
            env.Object,
            ConfigWithConnectionString("Host=h;Username=u;Password=p;Database=d"));

        var r = v.Validate(null, new BackupOptions
        {
            ExecutionAdapterKind = BackupExecutionAdapterKind.PgDump,
            ArtifactStagingRoot = "relative/backups",
            VerifyLogicalDumpFileOnDisk = true
        });

        Assert.True(r.Failed);
    }

    [Fact]
    public void Validate_PgDump_in_Production_missing_external_archive_fails()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var v = new BackupOptionsValidator(
            env.Object,
            ConfigWithConnectionString("Host=h;Username=u;Password=p;Database=d"));

        var r = v.Validate(null, new BackupOptions
        {
            ExecutionAdapterKind = BackupExecutionAdapterKind.PgDump,
            ArtifactStagingRoot = OperatingSystem.IsWindows() ? @"C:\RegkasseBackup" : "/var/regkasse-backup",
            ExternalArchiveRoot = null,
            VerifyLogicalDumpFileOnDisk = true
        });

        Assert.True(r.Failed);
    }

    [Fact]
    public void Validate_PgDump_in_Production_missing_connection_string_fails()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var v = new BackupOptionsValidator(env.Object, ConfigWithConnectionString(null));

        var r = v.Validate(null, new BackupOptions
        {
            ExecutionAdapterKind = BackupExecutionAdapterKind.PgDump,
            ArtifactStagingRoot = OperatingSystem.IsWindows() ? @"C:\RegkasseBackup" : "/var/regkasse-backup",
            VerifyLogicalDumpFileOnDisk = true
        });

        Assert.True(r.Failed);
    }

    [Fact]
    public void Validate_PgDump_in_Production_well_formed_succeeds()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var v = new BackupOptionsValidator(
            env.Object,
            ConfigWithConnectionString("Host=h;Username=u;Password=p;Database=d"));

        var r = v.Validate(null, new BackupOptions
        {
            ExecutionAdapterKind = BackupExecutionAdapterKind.PgDump,
            ArtifactStagingRoot = OperatingSystem.IsWindows() ? @"C:\RegkasseBackup" : "/var/regkasse-backup",
            ExternalArchiveRoot = OperatingSystem.IsWindows() ? @"D:\RegkasseArchive" : "/var/regkasse-archive",
            VerifyLogicalDumpFileOnDisk = true
        });

        Assert.False(r.Failed);
    }

    [Fact]
    public void Evaluate_Production_PgDump_valid_is_Healthy_with_real_dump_flag()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var snap = BackupConfigurationEvaluation.Evaluate(
            new BackupOptions
            {
                ExecutionAdapterKind = BackupExecutionAdapterKind.PgDump,
                ArtifactStagingRoot = OperatingSystem.IsWindows() ? @"C:\RegkasseBackup" : "/var/regkasse-backup",
                ExternalArchiveRoot = OperatingSystem.IsWindows() ? @"D:\RegkasseArchive" : "/var/regkasse-archive",
                VerifyLogicalDumpFileOnDisk = true,
                ExternalArchiveImmutabilityAcknowledged = true
            },
            env.Object,
            ConfigWithConnectionString("Host=h;Username=u;Password=p;Database=d"));

        Assert.Equal(BackupConfigurationHealthLevel.Healthy, snap.Level);
        Assert.True(snap.RealPostgreSqlLogicalDumpConfigured);
        Assert.Equal(BackupConfigurationEvaluation.BackupExecutionRealityPostgreSqlLogicalDump, snap.BackupExecutionReality);
        Assert.Null(snap.NonRealBackupAdapterAcknowledgmentConfigurationKey);
    }

    [Fact]
    public void Validate_ScheduledBackupEnabled_without_cron_fails()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        var v = new BackupOptionsValidator(env.Object, ConfigWithConnectionString(null));
        var r = v.Validate(null, new BackupOptions
        {
            ExecutionAdapterKind = BackupExecutionAdapterKind.Fake,
            ScheduledBackupEnabled = true
        });

        Assert.True(r.Failed);
    }

    [Fact]
    public void Validate_ScheduledBackupEnabled_with_invalid_cron_fails()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        var v = new BackupOptionsValidator(env.Object, ConfigWithConnectionString(null));
        var r = v.Validate(null, new BackupOptions
        {
            ExecutionAdapterKind = BackupExecutionAdapterKind.Fake,
            ScheduledBackupEnabled = true,
            ScheduledBackupCron = "not-a-cron"
        });

        Assert.True(r.Failed);
    }
}
