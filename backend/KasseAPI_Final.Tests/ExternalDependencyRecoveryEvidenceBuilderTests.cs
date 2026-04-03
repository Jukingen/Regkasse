using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.RestoreVerification;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ExternalDependencyRecoveryEvidenceBuilderTests
{
    [Fact]
    public void Build_returns_l6_domains_rollup_and_legacy_partial()
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FinanzOnline:DevTest:AllowEnqueueSmokeTest"] = "false",
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=x"
            })
            .Build();

        var tseMon = new Mock<IOptionsMonitor<TseOptions>>();
        tseMon.Setup(m => m.CurrentValue).Returns(new TseOptions { Mode = "Fake", TseMode = "Demo" });
        var backupMon = new Mock<IOptionsMonitor<BackupOptions>>();
        backupMon.Setup(m => m.CurrentValue).Returns(new BackupOptions { PgDumpExecutablePath = @"C:\pg\bin\pg_dump.exe" });
        var rvMon = new Mock<IOptionsMonitor<RestoreVerificationOptions>>();
        rvMon.Setup(m => m.CurrentValue).Returns(new RestoreVerificationOptions());
        var host = new Mock<IHostEnvironment>();
        host.Setup(h => h.EnvironmentName).Returns(Environments.Development);

        var sut = new ExternalDependencyRecoveryEvidenceBuilder(cfg, tseMon.Object, backupMon.Object, rvMon.Object, host.Object);
        var block = sut.Build();

        Assert.Equal(1, block.L6EvidenceSchemaVersion);
        Assert.NotNull(block.Rollup);
        Assert.Equal(5, block.Domains.Count);
        Assert.Equal(RecoveryProofOutcome.Partial, block.OverallOutcome);
        Assert.Equal(ExternalDependencyProofState.NotProven, block.Rollup!.OverallState);
        Assert.Contains(block.Domains, d => d.Domain == ExternalDependencyRecoveryDomain.TseDeviceVendor);
        Assert.Contains(block.Domains, d => d.Domain == ExternalDependencyRecoveryDomain.BackupTooling
                                              && d.State == ExternalDependencyProofState.NotImplemented);
        Assert.NotEmpty(block.Checks);
    }
}
