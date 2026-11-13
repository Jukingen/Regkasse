using KasseAPI_Final.Services.Rksv;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RksvEnvironmentServiceTests
{
    private static RksvEnvironmentService CreateService(
        IReadOnlyDictionary<string, string?>? configValues = null,
        string environmentName = "Production")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues ?? new Dictionary<string, string?>())
            .Build();
        var env = Mock.Of<IHostEnvironment>(h => h.EnvironmentName == environmentName);

        return new RksvEnvironmentService(config, env);
    }

    [Fact]
    public void IsDemoMode_ReturnsTrue_InDevelopment()
    {
        var service = CreateService(environmentName: Environments.Development);

        Assert.True(service.IsDemoMode());
        Assert.False(service.IsProductionMode());
        Assert.Equal("🧪 DEMO / TEST", service.GetEnvironmentDisplayName());
    }

    [Fact]
    public void IsDemoMode_ReturnsTrue_WhenRksvModeIsDemo()
    {
        var service = CreateService(new Dictionary<string, string?> { ["RKSV:Mode"] = "Demo" });

        Assert.True(service.IsDemoMode());
    }

    [Fact]
    public void IsProductionMode_ReturnsTrue_WhenProductionHostAndRksvProduction()
    {
        var service = CreateService(new Dictionary<string, string?>
        {
            ["RKSV:Mode"] = "Production",
            ["RKSV:ShowDemoLabel"] = "false"
        });

        Assert.False(service.IsDemoMode());
        Assert.True(service.IsProductionMode());
        Assert.Equal("🚀 PRODUCTION", service.GetEnvironmentDisplayName());
    }

    [Fact]
    public void IsTseSimulated_ReturnsTrue_WhenTseModeSimulation()
    {
        var service = CreateService(new Dictionary<string, string?> { ["RKSV:TseMode"] = "Simulation" });

        Assert.True(service.IsTseSimulated());
    }

    [Fact]
    public void ShowDemoLabel_ReturnsFalse_WhenDemoModeButShowDemoLabelFalse()
    {
        var service = CreateService(new Dictionary<string, string?>
        {
            ["RKSV:Mode"] = "Demo",
            ["RKSV:ShowDemoLabel"] = "false"
        });

        Assert.True(service.IsDemoMode());
        Assert.False(service.ShowDemoLabel());
    }

    [Fact]
    public void ShowDemoLabel_DefaultsTrue_InDemoMode()
    {
        var service = CreateService(new Dictionary<string, string?> { ["RKSV:Mode"] = "Demo" });

        Assert.True(service.ShowDemoLabel());
    }

    [Fact]
    public void GetTseStatusDisplay_ReturnsSimulated_InDemoMode()
    {
        var service = CreateService(environmentName: Environments.Development);

        Assert.Equal("TSE: SIMULIERT (NUR TEST)", service.GetTseStatusDisplay());
        Assert.Equal("TSE SIMULIERT", service.GetTseStatusBadge());
    }

    [Fact]
    public void GetTseStatusDisplay_ReturnsActive_InProduction()
    {
        var service = CreateService(new Dictionary<string, string?>
        {
            ["RKSV:Mode"] = "Production",
            ["RKSV:TseMode"] = "Real"
        });

        Assert.Equal("TSE: AKTIV ✅", service.GetTseStatusDisplay());
        Assert.Equal("TSE AKTIV", service.GetTseStatusBadge());
    }

    [Fact]
    public void GetRksvFooter_ReturnsDemoDisclaimer_InDevelopment()
    {
        var service = CreateService(environmentName: Environments.Development);
        var footer = service.GetRksvFooter();

        Assert.Contains("DEMO / NICHT FISKAL", footer, StringComparison.Ordinal);
        Assert.Contains("Testzwecken", footer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SIMULIERT", footer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetRksvFooter_ReturnsProductionDisclaimer_InProduction()
    {
        var service = CreateService(new Dictionary<string, string?> { ["RKSV:Mode"] = "Production" });
        var footer = service.GetRksvFooter();

        Assert.Contains("Registrierkassensicherheitsverordnung", footer, StringComparison.Ordinal);
        Assert.Contains("fiskalisch gültig", footer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GEPRÜFT", footer, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Phase 1 validation matrix: Demo vs Production environment presentation.
    /// </summary>
    [Theory]
    [InlineData("Development", "Demo", "Simulation", true, false, "TSE: SIMULIERT (NUR TEST)", "TSE SIMULIERT", "DEMO / NICHT FISKAL", false)]
    [InlineData("Production", "Production", "Real", false, true, "TSE: AKTIV ✅", "TSE AKTIV", "Registrierkassensicherheitsverordnung", true)]
    public void EnvironmentValidationMatrix_DemoVsProduction(
        string hostEnvironment,
        string rksvMode,
        string tseMode,
        bool expectDemoLabel,
        bool expectRksvKonform,
        string expectedTseStatus,
        string expectedTseBadge,
        string expectedFooterFragment,
        bool expectProductionMode)
    {
        var service = CreateService(
            new Dictionary<string, string?>
            {
                ["RKSV:Mode"] = rksvMode,
                ["RKSV:TseMode"] = tseMode,
            },
            hostEnvironment);

        Assert.Equal(expectDemoLabel, service.ShowDemoLabel());
        Assert.Equal(expectProductionMode, service.IsProductionMode());
        Assert.Equal(!expectProductionMode, service.IsDemoMode());
        Assert.Equal(expectedTseStatus, service.GetTseStatusDisplay());
        Assert.Equal(expectedTseBadge, service.GetTseStatusBadge());
        Assert.Contains(expectedFooterFragment, service.GetRksvFooter(), StringComparison.Ordinal);

        if (expectRksvKonform)
        {
            Assert.DoesNotContain("DEMO / NICHT FISKAL", service.GetRksvFooter(), StringComparison.Ordinal);
        }
        else
        {
            Assert.DoesNotContain("fiskalisch gültig", service.GetRksvFooter(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
