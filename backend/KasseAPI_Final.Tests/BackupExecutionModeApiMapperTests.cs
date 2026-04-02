using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.Backup;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;
using Environments = Microsoft.Extensions.Hosting.Environments;

namespace KasseAPI_Final.Tests;

public sealed class BackupExecutionModeApiMapperTests
{
    [Theory]
    [InlineData("Fake", AdminBackupRuntimeExecutionMode.SimulatedFake)]
    [InlineData("realpgdump", AdminBackupRuntimeExecutionMode.PostgreSqlPgDump)]
    [InlineData("RealPgDump", AdminBackupRuntimeExecutionMode.PostgreSqlPgDump)]
    [InlineData("UseConfigurationDefault", AdminBackupRuntimeExecutionMode.InheritFromConfiguration)]
    [InlineData("inherit", AdminBackupRuntimeExecutionMode.InheritFromConfiguration)]
    [InlineData("PostgreSqlPgDump", AdminBackupRuntimeExecutionMode.PostgreSqlPgDump)]
    [InlineData("SimulatedFake", AdminBackupRuntimeExecutionMode.SimulatedFake)]
    public void TryParseAdminMode_accepts_user_facing_and_internal_names(string raw, AdminBackupRuntimeExecutionMode expected)
    {
        Assert.True(BackupExecutionModeApiMapper.TryParseAdminMode(raw, out var mode, out var err));
        Assert.Null(err);
        Assert.Equal(expected, mode);
    }

    [Fact]
    public void TryParseAdminMode_rejects_garbage()
    {
        Assert.False(BackupExecutionModeApiMapper.TryParseAdminMode("not-a-mode", out _, out var err));
        Assert.NotNull(err);
    }

    [Fact]
    public void ToUserFacingMode_maps_internal_enum()
    {
        Assert.Equal(BackupExecutionModeApiMapper.UserFacingFake,
            BackupExecutionModeApiMapper.ToUserFacingMode(AdminBackupRuntimeExecutionMode.SimulatedFake));
        Assert.Equal(BackupExecutionModeApiMapper.UserFacingRealPgDump,
            BackupExecutionModeApiMapper.ToUserFacingMode(AdminBackupRuntimeExecutionMode.PostgreSqlPgDump));
        Assert.Equal(BackupExecutionModeApiMapper.UserFacingUseConfigurationDefault,
            BackupExecutionModeApiMapper.ToUserFacingMode(AdminBackupRuntimeExecutionMode.InheritFromConfiguration));
    }

    [Fact]
    public void RecommendedFallback_when_real_unhealthy_suggests_use_config_default()
    {
        var fb = BackupExecutionModeApiMapper.RecommendedFallbackUserFacingMode(
            AdminBackupRuntimeExecutionMode.PostgreSqlPgDump,
            effectiveRunnable: false);
        Assert.Equal(BackupExecutionModeApiMapper.UserFacingUseConfigurationDefault, fb);
    }

    [Fact]
    public void RecommendedFallback_null_when_healthy_or_not_real()
    {
        Assert.Null(BackupExecutionModeApiMapper.RecommendedFallbackUserFacingMode(
            AdminBackupRuntimeExecutionMode.PostgreSqlPgDump,
            effectiveRunnable: true));
        Assert.Null(BackupExecutionModeApiMapper.RecommendedFallbackUserFacingMode(
            AdminBackupRuntimeExecutionMode.SimulatedFake,
            effectiveRunnable: false));
    }

    [Fact]
    public void BuildSelectableModes_RealPgDump_row_not_selectable_when_hypothetical_Unhealthy()
    {
        var opts = new BackupOptions();
        var env = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Development);
        var hypo = new BackupConfigurationHealthSnapshot
        {
            Level = BackupConfigurationHealthLevel.Unhealthy,
            Issues = new[] { "conn-string-missing", "staging-unwritable" },
            Diagnostics = Array.Empty<BackupConfigurationDiagnostic>(),
            EffectiveAdapterKind = BackupExecutionAdapterKind.PgDump,
            ConfigurationExecutionAdapterKind = BackupExecutionAdapterKind.Fake,
            AdminRuntimeExecutionMode = AdminBackupRuntimeExecutionMode.PostgreSqlPgDump
        };

        var rows = BackupExecutionModeApiMapper.BuildSelectableModes(opts, env, hypo);
        var real = Assert.Single(rows, r => r.UserFacingMode == BackupExecutionModeApiMapper.UserFacingRealPgDump);
        Assert.False(real.Selectable);
        Assert.NotNull(real.BlockReason);
        Assert.Contains("conn-string-missing", real.BlockReason, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSelectableModes_RealPgDump_row_selectable_when_hypothetical_Healthy()
    {
        var opts = new BackupOptions();
        var env = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Development);
        var hypo = new BackupConfigurationHealthSnapshot
        {
            Level = BackupConfigurationHealthLevel.Healthy,
            Issues = Array.Empty<string>(),
            Diagnostics = Array.Empty<BackupConfigurationDiagnostic>(),
            EffectiveAdapterKind = BackupExecutionAdapterKind.PgDump,
            ConfigurationExecutionAdapterKind = BackupExecutionAdapterKind.Fake,
            AdminRuntimeExecutionMode = AdminBackupRuntimeExecutionMode.PostgreSqlPgDump
        };

        var rows = BackupExecutionModeApiMapper.BuildSelectableModes(opts, env, hypo);
        var real = Assert.Single(rows, r => r.UserFacingMode == BackupExecutionModeApiMapper.UserFacingRealPgDump);
        Assert.True(real.Selectable);
        Assert.Null(real.BlockReason);
    }

    [Fact]
    public void BuildSelectableModes_Fake_blocked_in_production_like_until_ack_flag()
    {
        var opts = new BackupOptions { AcknowledgeFakeBackupAdapterOutsideDevelopment = false };
        var env = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Production);
        var hypoHealthy = new BackupConfigurationHealthSnapshot { Level = BackupConfigurationHealthLevel.Healthy };

        var rows = BackupExecutionModeApiMapper.BuildSelectableModes(opts, env, hypoHealthy);
        var fake = Assert.Single(rows, r => r.UserFacingMode == BackupExecutionModeApiMapper.UserFacingFake);
        Assert.False(fake.Selectable);
        Assert.Contains("AcknowledgeFakeBackupAdapterOutsideDevelopment", fake.BlockReason ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public void FilterRealModeBlockingDiagnostics_keeps_errors_and_warnings_only()
    {
        var hypo = new BackupConfigurationHealthSnapshot
        {
            Diagnostics = new[]
            {
                new BackupConfigurationDiagnostic
                {
                    Code = "E1",
                    Severity = BackupConfigurationDiagnosticSeverity.Error,
                    Message = "blocking"
                },
                new BackupConfigurationDiagnostic
                {
                    Code = "W1",
                    Severity = BackupConfigurationDiagnosticSeverity.Warning,
                    Message = "warn"
                },
                new BackupConfigurationDiagnostic
                {
                    Code = "I1",
                    Severity = BackupConfigurationDiagnosticSeverity.Information,
                    Message = "info"
                }
            }
        };

        var filtered = BackupExecutionModeApiMapper.FilterRealModeBlockingDiagnostics(hypo);
        Assert.Equal(2, filtered.Count);
        Assert.Contains(filtered, d => d.Code == "E1");
        Assert.Contains(filtered, d => d.Code == "W1");
    }
}
