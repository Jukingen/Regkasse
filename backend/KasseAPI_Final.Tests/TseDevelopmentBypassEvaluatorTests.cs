using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Rksv;
using KasseAPI_Final.Services.Tse;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseDevelopmentBypassEvaluatorTests
{
    private static IHostEnvironment Env(string name) =>
        Mock.Of<IHostEnvironment>(h => h.EnvironmentName == name);

    private static RksvRuntimeSnapshot Overlay(
        string tseMode,
        bool bypassTseInDevelopment = false,
        string mode = RksvRuntimeConfig.ModeDemo) =>
        new(
            Mode: mode,
            TseMode: tseMode,
            FinanzOnlineMode: RksvRuntimeConfig.IntegrationSimulation,
            ShowDemoLabel: true,
            OverlayPersisted: true,
            Source: RksvRuntimeSnapshot.SourceDatabase,
            UpdatedAtUtc: DateTime.UtcNow,
            UpdatedByUserId: null,
            BypassTseInDevelopment: bypassTseInDevelopment);

    [Fact]
    public void Default_DoesNotBypass_InDevelopment()
    {
        var bypass = TseDevelopmentBypassEvaluator.ShouldBypassTseHealth(
            Env(Environments.Development),
            new DevelopmentOptions(),
            developmentModeEnabled: true,
            developmentModeBypassTseCheck: false,
            overlay: Overlay(RksvRuntimeConfig.IntegrationSimulation));

        Assert.False(bypass);
    }

    [Fact]
    public void NeverBypasses_OutsideDevelopment()
    {
        var bypass = TseDevelopmentBypassEvaluator.ShouldBypassTseHealth(
            Env(Environments.Production),
            new DevelopmentOptions { BypassTseInDevelopment = true },
            developmentModeEnabled: true,
            developmentModeBypassTseCheck: true,
            overlay: Overlay(RksvRuntimeConfig.IntegrationSimulation, bypassTseInDevelopment: true));

        Assert.False(bypass);
    }

    [Fact]
    public void AppsettingsFlag_Bypasses_WhenOverlayIsSimulation()
    {
        var bypass = TseDevelopmentBypassEvaluator.ShouldBypassTseHealth(
            Env(Environments.Development),
            new DevelopmentOptions { BypassTseInDevelopment = true },
            developmentModeEnabled: false,
            developmentModeBypassTseCheck: false,
            overlay: Overlay(RksvRuntimeConfig.IntegrationSimulation));

        Assert.True(bypass);
    }

    [Fact]
    public void OverlayFlag_Bypasses_WhenTseModeIsSimulation()
    {
        var bypass = TseDevelopmentBypassEvaluator.ShouldBypassTseHealth(
            Env(Environments.Development),
            new DevelopmentOptions(),
            developmentModeEnabled: false,
            developmentModeBypassTseCheck: false,
            overlay: Overlay(RksvRuntimeConfig.IntegrationSimulation, bypassTseInDevelopment: true));

        Assert.True(bypass);
    }

    [Fact]
    public void OverlayTseModeReal_NeverBypasses_EvenWhenAllFlagsOn()
    {
        var bypass = TseDevelopmentBypassEvaluator.ShouldBypassTseHealth(
            Env(Environments.Development),
            new DevelopmentOptions { BypassTseInDevelopment = true },
            developmentModeEnabled: true,
            developmentModeBypassTseCheck: true,
            overlay: Overlay(
                RksvRuntimeConfig.IntegrationReal,
                bypassTseInDevelopment: true,
                mode: RksvRuntimeConfig.ModeProduction));

        Assert.False(bypass);
    }

    [Fact]
    public void DevelopmentModeToggle_Bypasses_WhenOverlayIsSimulation()
    {
        var bypass = TseDevelopmentBypassEvaluator.ShouldBypassTseHealth(
            Env(Environments.Development),
            new DevelopmentOptions(),
            developmentModeEnabled: true,
            developmentModeBypassTseCheck: true,
            overlay: Overlay(RksvRuntimeConfig.IntegrationSimulation));

        Assert.True(bypass);
    }
}
