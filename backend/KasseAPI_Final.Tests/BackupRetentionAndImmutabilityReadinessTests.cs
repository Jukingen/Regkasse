using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.Backup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// DR hazırlığı: saklama alanları doğrulaması + harici arşiv immutability gereksinimi.
/// </summary>
public sealed class BackupRetentionAndImmutabilityReadinessTests
{
    private static IConfiguration ConfigWithConnectionString(string? connectionString, string connectionName = "DefaultConnection")
    {
        var dict = new Dictionary<string, string?>();
        if (connectionString != null)
            dict[$"ConnectionStrings:{connectionName}"] = connectionString;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private static BackupOptions PgDumpProductionBase() => new()
    {
        ExecutionAdapterKind = BackupExecutionAdapterKind.PgDump,
        ArtifactStagingRoot = OperatingSystem.IsWindows() ? @"C:\RegkasseBackup" : "/var/regkasse-backup",
        ExternalArchiveRoot = OperatingSystem.IsWindows() ? @"D:\RegkasseArchive" : "/var/regkasse-archive",
        VerifyLogicalDumpFileOnDisk = true
    };

    [Fact]
    public void Evaluate_ProductionPgDump_immutable_required_without_ack_is_Unhealthy()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var opts = PgDumpProductionBase();
        opts.RequireExternalArchiveImmutableTarget = true;
        opts.ExternalArchiveImmutabilityAcknowledged = false;

        var snap = BackupConfigurationEvaluation.Evaluate(opts, env.Object, ConfigWithConnectionString("Host=h;Username=u;Password=p;Database=d"));

        Assert.Equal(BackupConfigurationHealthLevel.Unhealthy, snap.Level);
        Assert.Contains(snap.Issues, i => i.Contains("ExternalArchiveImmutabilityAcknowledged", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_ProductionPgDump_immutable_required_with_ack_is_not_blocked_by_immutability()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var opts = PgDumpProductionBase();
        opts.RequireExternalArchiveImmutableTarget = true;
        opts.ExternalArchiveImmutabilityAcknowledged = true;

        var snap = BackupConfigurationEvaluation.Evaluate(opts, env.Object, ConfigWithConnectionString("Host=h;Username=u;Password=p;Database=d"));

        Assert.DoesNotContain(snap.Issues, i => i.Contains("ExternalArchiveImmutabilityAcknowledged", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_Development_immutable_required_without_ack_still_permissive()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        var opts = PgDumpProductionBase();
        opts.RequireExternalArchiveImmutableTarget = true;
        opts.ExternalArchiveImmutabilityAcknowledged = false;

        var snap = BackupConfigurationEvaluation.Evaluate(opts, env.Object, ConfigWithConnectionString(null));

        Assert.DoesNotContain(snap.Issues, i => i.Contains("immutable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_retention_ReportOnly_without_days_fails()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        var v = new BackupOptionsValidator(env.Object, ConfigWithConnectionString(null));
        var r = v.Validate(null, new BackupOptions
        {
            ExecutionAdapterKind = BackupExecutionAdapterKind.Fake,
            RetentionPolicyMode = BackupRetentionPolicyMode.ReportOnly,
            ArtifactRetentionDays = null
        });

        Assert.True(r.Failed);
    }

    [Fact]
    public void Validate_retention_ReportOnly_with_days_below_min_fails()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        var v = new BackupOptionsValidator(env.Object, ConfigWithConnectionString(null));
        var r = v.Validate(null, new BackupOptions
        {
            ExecutionAdapterKind = BackupExecutionAdapterKind.Fake,
            RetentionPolicyMode = BackupRetentionPolicyMode.ReportOnly,
            ArtifactRetentionDays = 3
        });

        Assert.True(r.Failed);
    }

    [Fact]
    public void Validate_retention_ReportOnly_with_valid_days_succeeds_in_Dev()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        var v = new BackupOptionsValidator(env.Object, ConfigWithConnectionString(null));
        var r = v.Validate(null, new BackupOptions
        {
            ExecutionAdapterKind = BackupExecutionAdapterKind.Fake,
            RetentionPolicyMode = BackupRetentionPolicyMode.ReportOnly,
            ArtifactRetentionDays = 30
        });

        Assert.False(r.Failed);
    }

    [Fact]
    public void Validate_retention_Disabled_with_artifact_days_set_fails()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        var v = new BackupOptionsValidator(env.Object, ConfigWithConnectionString(null));
        var r = v.Validate(null, new BackupOptions
        {
            ExecutionAdapterKind = BackupExecutionAdapterKind.Fake,
            RetentionPolicyMode = BackupRetentionPolicyMode.Disabled,
            ArtifactRetentionDays = 30
        });

        Assert.True(r.Failed);
    }

    [Fact]
    public void Evaluate_ExecutionPlanned_is_Degraded()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        var snap = BackupConfigurationEvaluation.Evaluate(
            new BackupOptions
            {
                ExecutionAdapterKind = BackupExecutionAdapterKind.Fake,
                RetentionPolicyMode = BackupRetentionPolicyMode.ExecutionPlanned,
                ArtifactRetentionDays = 90
            },
            env.Object);

        Assert.Equal(BackupConfigurationHealthLevel.Degraded, snap.Level);
        Assert.Contains(snap.Issues, i => i.Contains("ExecutionPlanned", StringComparison.Ordinal));
    }

    [Fact]
    public void BackupRetentionOptionsValidation_valid_bounds()
    {
        Assert.Null(BackupRetentionOptionsValidation.Validate(new BackupOptions { RetentionPolicyMode = BackupRetentionPolicyMode.Disabled }));
        Assert.NotNull(BackupRetentionOptionsValidation.Validate(new BackupOptions { RetentionPolicyMode = BackupRetentionPolicyMode.ReportOnly }));
        Assert.Null(BackupRetentionOptionsValidation.Validate(new BackupOptions
        {
            RetentionPolicyMode = BackupRetentionPolicyMode.ReportOnly,
            ArtifactRetentionDays = 7
        }));
        Assert.Null(BackupRetentionOptionsValidation.Validate(new BackupOptions
        {
            RetentionPolicyMode = BackupRetentionPolicyMode.ReportOnly,
            ArtifactRetentionDays = 3650
        }));
        Assert.NotNull(BackupRetentionOptionsValidation.Validate(new BackupOptions
        {
            RetentionPolicyMode = BackupRetentionPolicyMode.ReportOnly,
            ArtifactRetentionDays = 3651
        }));
    }
}
