using KasseAPI_Final.Models;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FinanzOnlineHealthCheckTests
{
    private static IOptionsMonitor<T> MonitorOf<T>(T value)
        where T : class
    {
        var mock = new Mock<IOptionsMonitor<T>>();
        mock.Setup(m => m.CurrentValue).Returns(value);
        return mock.Object;
    }

    private static IHostEnvironment Env(string name)
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(name);
        return env.Object;
    }

    private static IConfiguration Config(params (string Key, string? Value)[] pairs) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.ToDictionary(p => p.Key, p => p.Value))
            .Build();

    private static HealthChecks.FinanzOnlineHealthCheck CreateCheck(
        string environmentName,
        bool useSimulation,
        TseOptions? tseOptions = null) =>
        new(
            Env(environmentName),
            Config(("FinanzOnline:Session:UseSimulation", useSimulation ? "true" : "false")),
            MonitorOf(tseOptions ?? new TseOptions { EnforceProductionLockInStaging = true }),
            MonitorOf(new FinanzOnlineSessionOptions { UseSimulation = useSimulation }));

    [Fact]
    public async Task CheckHealthAsync_Development_Simulation_IsHealthy()
    {
        var check = CreateCheck(Environments.Development, useSimulation: true);
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("simulation", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckHealthAsync_Production_Simulation_IsUnhealthy()
    {
        var check = CreateCheck(Environments.Production, useSimulation: true);
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenSimulationDisabled_ReturnsHealthy()
    {
        var check = CreateCheck(Environments.Production, useSimulation: false);
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }
}
