using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Rksv;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RksvRuntimeOverlayEnvironmentTests
{
    private static RksvEnvironmentService CreateService(
        RksvRuntimeSnapshot overlay,
        string hostEnvironment = "Development",
        IReadOnlyDictionary<string, string?>? config = null)
    {
        var runtime = new Mock<IRksvRuntimeConfigService>();
        runtime.Setup(r => r.GetEffective()).Returns(overlay);
        var env = Mock.Of<IHostEnvironment>(h => h.EnvironmentName == hostEnvironment);
        return new RksvEnvironmentService(
            new ConfigurationBuilder().AddInMemoryCollection(config ?? new Dictionary<string, string?>
            {
                ["RKSV:Mode"] = "Demo",
                ["RKSV:ShowDemoLabel"] = "true",
            }).Build(),
            env,
            runtime.Object);
    }

    [Fact]
    public void Overlay_Production_HidesDemoLabel_OnDevelopmentHost()
    {
        var service = CreateService(new RksvRuntimeSnapshot(
            Mode: RksvRuntimeConfig.ModeProduction,
            TseMode: RksvRuntimeConfig.IntegrationSimulation,
            FinanzOnlineMode: RksvRuntimeConfig.IntegrationSimulation,
            ShowDemoLabel: false,
            OverlayPersisted: true,
            Source: RksvRuntimeSnapshot.SourceDatabase,
            UpdatedAtUtc: DateTime.UtcNow,
            UpdatedByUserId: null));

        Assert.False(service.IsDemoMode());
        Assert.True(service.IsProductionMode());
        Assert.False(service.ShowDemoLabel());
        Assert.DoesNotContain("DEMO / NICHT FISKAL", service.GetRksvFooter(), StringComparison.Ordinal);
        Assert.True(service.IsTseSimulated());
    }

    [Fact]
    public void Overlay_Production_RealTse_IsNotSimulated_OnDevelopmentHost()
    {
        var service = CreateService(new RksvRuntimeSnapshot(
            Mode: RksvRuntimeConfig.ModeProduction,
            TseMode: RksvRuntimeConfig.IntegrationReal,
            FinanzOnlineMode: RksvRuntimeConfig.IntegrationReal,
            ShowDemoLabel: false,
            OverlayPersisted: true,
            Source: RksvRuntimeSnapshot.SourceDatabase,
            UpdatedAtUtc: DateTime.UtcNow,
            UpdatedByUserId: null,
            BypassTseInDevelopment: true));

        Assert.False(service.IsDemoMode());
        Assert.False(service.IsTseSimulated());
        Assert.False(service.ShowDemoLabel());
    }

    [Fact]
    public void Overlay_Demo_ShowsDemoLabel()
    {
        var service = CreateService(new RksvRuntimeSnapshot(
            Mode: RksvRuntimeConfig.ModeDemo,
            TseMode: RksvRuntimeConfig.IntegrationSimulation,
            FinanzOnlineMode: RksvRuntimeConfig.IntegrationSimulation,
            ShowDemoLabel: true,
            OverlayPersisted: true,
            Source: RksvRuntimeSnapshot.SourceDatabase,
            UpdatedAtUtc: DateTime.UtcNow,
            UpdatedByUserId: null));

        Assert.True(service.IsDemoMode());
        Assert.True(service.ShowDemoLabel());
        Assert.Contains("DEMO / NICHT FISKAL", service.GetRksvFooter(), StringComparison.Ordinal);
    }
}
