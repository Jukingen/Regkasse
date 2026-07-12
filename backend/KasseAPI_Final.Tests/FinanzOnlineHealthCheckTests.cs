using KasseAPI_Final.Services.FinanzOnlineIntegration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FinanzOnlineHealthCheckTests
{
    private static IOptionsMonitor<FinanzOnlineSessionOptions> MonitorOf(FinanzOnlineSessionOptions value)
    {
        var mock = new Mock<IOptionsMonitor<FinanzOnlineSessionOptions>>();
        mock.Setup(m => m.CurrentValue).Returns(value);
        return mock.Object;
    }

    [Fact]
    public async Task CheckHealthAsync_WhenSimulationEnabled_ReturnsDegraded()
    {
        var check = new HealthChecks.FinanzOnlineHealthCheck(
            MonitorOf(new FinanzOnlineSessionOptions { UseSimulation = true }));

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("simulation", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenSimulationDisabled_ReturnsHealthy()
    {
        var check = new HealthChecks.FinanzOnlineHealthCheck(
            MonitorOf(new FinanzOnlineSessionOptions { UseSimulation = false }));

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }
}
