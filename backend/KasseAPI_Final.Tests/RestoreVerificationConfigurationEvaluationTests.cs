using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.RestoreVerification;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RestoreVerificationConfigurationEvaluationTests
{
    [Fact]
    public void Evaluate_non_dev_lock_disabled_is_degraded()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Staging);
        var opts = new RestoreVerificationOptions
        {
            WorkerEnabled = true,
            OrchestratorDistributedLockEnabled = false,
            OrchestratorPollingInterval = TimeSpan.FromSeconds(15)
        };

        var snap = RestoreVerificationConfigurationEvaluation.Evaluate(opts, env.Object);
        Assert.Equal(RestoreVerificationConfigurationHealthLevel.Degraded, snap.Level);
        Assert.Contains(snap.Issues, i => i.Contains("OrchestratorDistributedLockEnabled=false", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_development_lock_disabled_still_healthy()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        var opts = new RestoreVerificationOptions
        {
            WorkerEnabled = true,
            OrchestratorDistributedLockEnabled = false,
            OrchestratorPollingInterval = TimeSpan.FromSeconds(15)
        };

        var snap = RestoreVerificationConfigurationEvaluation.Evaluate(opts, env.Object);
        Assert.Equal(RestoreVerificationConfigurationHealthLevel.Healthy, snap.Level);
    }
}
