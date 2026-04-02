using KasseAPI_Final.Services.Backup;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupConfigurationHealthLevelAggregatorTests
{
    [Fact]
    public void CombineWithDiagnostics_keeps_healthy_when_no_additional()
    {
        var r = BackupConfigurationHealthLevelAggregator.CombineWithDiagnostics(
            BackupConfigurationHealthLevel.Healthy,
            Array.Empty<BackupConfigurationDiagnostic>());
        Assert.Equal(BackupConfigurationHealthLevel.Healthy, r);
    }

    [Fact]
    public void CombineWithDiagnostics_warning_bumps_healthy_to_degraded()
    {
        var r = BackupConfigurationHealthLevelAggregator.CombineWithDiagnostics(
            BackupConfigurationHealthLevel.Healthy,
            new[]
            {
                new BackupConfigurationDiagnostic
                {
                    Code = "X",
                    Severity = BackupConfigurationDiagnosticSeverity.Warning,
                    Message = "m"
                }
            });
        Assert.Equal(BackupConfigurationHealthLevel.Degraded, r);
    }

    [Fact]
    public void CombineWithDiagnostics_error_makes_unhealthy_even_from_healthy()
    {
        var r = BackupConfigurationHealthLevelAggregator.CombineWithDiagnostics(
            BackupConfigurationHealthLevel.Healthy,
            new[]
            {
                new BackupConfigurationDiagnostic
                {
                    Code = "X",
                    Severity = BackupConfigurationDiagnosticSeverity.Error,
                    Message = "m"
                }
            });
        Assert.Equal(BackupConfigurationHealthLevel.Unhealthy, r);
    }

    [Fact]
    public void CombineWithDiagnostics_preserves_unhealthy_when_no_extra_diagnostics()
    {
        var r = BackupConfigurationHealthLevelAggregator.CombineWithDiagnostics(
            BackupConfigurationHealthLevel.Unhealthy,
            Array.Empty<BackupConfigurationDiagnostic>());
        Assert.Equal(BackupConfigurationHealthLevel.Unhealthy, r);
    }
}
