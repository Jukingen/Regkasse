using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.RestoreVerification;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;

namespace KasseAPI_Final.Tests;

internal static class RestoreVerificationTestDoubles
{
    /// <summary>Gerçek <see cref="ExternalDependencyRecoveryEvidenceBuilder"/> — Moq varsayılanı <c>Build()</c> için null döner.</summary>
    public static IExternalDependencyRecoveryEvidenceBuilder ExternalDependencyEvidence()
    {
        var tseMon = new Mock<IOptionsMonitor<TseOptions>>();
        tseMon.Setup(m => m.CurrentValue).Returns(new TseOptions { Mode = "Fake", TseMode = "Demo" });
        var backupMon = new Mock<IOptionsMonitor<BackupOptions>>();
        backupMon.Setup(m => m.CurrentValue).Returns(new BackupOptions());
        var rvMon = new Mock<IOptionsMonitor<RestoreVerificationOptions>>();
        rvMon.Setup(m => m.CurrentValue).Returns(new RestoreVerificationOptions());
        var host = new Mock<IHostEnvironment>();
        host.Setup(h => h.EnvironmentName).Returns(Environments.Development);
        return new ExternalDependencyRecoveryEvidenceBuilder(
            new ConfigurationBuilder().AddInMemoryCollection().Build(),
            tseMon.Object,
            backupMon.Object,
            rvMon.Object,
            host.Object);
    }
}
