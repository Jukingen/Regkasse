using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Rksv;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiscalEnvironmentResolverTests
{
    private static IRksvEnvironmentService CreateRksvEnvironment(
        IReadOnlyDictionary<string, string?>? configValues = null,
        string environmentName = "Production") =>
        new RksvEnvironmentService(
            new ConfigurationBuilder()
                .AddInMemoryCollection(configValues ?? new Dictionary<string, string?>())
                .Build(),
            Mock.Of<IHostEnvironment>(h => h.EnvironmentName == environmentName));

    [Fact]
    public void Resolve_ReturnsDemo_InDevelopment()
    {
        var env = Mock.Of<IHostEnvironment>(h => h.EnvironmentName == Environments.Development);
        var result = FiscalEnvironmentResolver.Resolve(
            env,
            new TseOptions { Mode = "Real", TseMode = "Device" },
            rksvEnvironment: CreateRksvEnvironment(environmentName: Environments.Development));

        Assert.True(result.IsDemoFiscal);
        Assert.Equal("Demo", result.EnvironmentName);
        Assert.Equal("TSE SIMULIERT", result.TseStatusBadge);
        Assert.Equal("TSE: SIMULIERT (NUR TEST)", result.TseStatusDisplay);
        Assert.Contains("DEMO", result.RksvFooterLabel, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_ReturnsProduction_InProductionWithRealTse()
    {
        var env = Mock.Of<IHostEnvironment>(h => h.EnvironmentName == Environments.Production);
        var result = FiscalEnvironmentResolver.Resolve(
            env,
            new TseOptions { Mode = "Real", TseMode = "Device" },
            rksvEnvironment: CreateRksvEnvironment());

        Assert.False(result.IsDemoFiscal);
        Assert.Equal("Production", result.EnvironmentName);
        Assert.Equal("TSE AKTIV", result.TseStatusBadge);
        Assert.Equal("TSE: AKTIV ✅", result.TseStatusDisplay);
    }

    [Fact]
    public void BuildClosingQrPayload_UsesDemoMarker_WhenDemoFiscal()
    {
        var payload = FiscalEnvironmentResolver.BuildClosingQrPayload(
            isDemoFiscal: true,
            tseSignature: "a.b.c",
            businessDate: new DateTime(2026, 6, 11),
            totalAmount: 120.5m);

        Assert.StartsWith("NON_FISCAL_DEMO_DAILY_", payload, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_ReturnsDemo_WhenRksvModeConfigIsDemo()
    {
        var env = Mock.Of<IHostEnvironment>(h => h.EnvironmentName == Environments.Production);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["RKSV:Mode"] = "Demo" })
            .Build();

        var result = FiscalEnvironmentResolver.Resolve(
            env,
            new TseOptions { Mode = "Real", TseMode = "Device" },
            config,
            rksvEnvironment: CreateRksvEnvironment(new Dictionary<string, string?> { ["RKSV:Mode"] = "Demo" }));

        Assert.True(result.IsDemoFiscal);
    }

    [Fact]
    public void Resolve_ReturnsProduction_WhenRksvOptionsProductionAndRealTse()
    {
        var env = Mock.Of<IHostEnvironment>(h => h.EnvironmentName == Environments.Production);
        var rksv = new RksvOptions
        {
            Mode = "Production",
            TseMode = "Real",
            FinanzOnlineMode = "Real",
            ShowDemoLabel = false
        };

        var result = FiscalEnvironmentResolver.Resolve(
            env,
            new TseOptions { Mode = "Real", TseMode = "Device" },
            rksvOptions: rksv,
            rksvEnvironment: CreateRksvEnvironment(new Dictionary<string, string?> { ["RKSV:Mode"] = "Production" }));

        Assert.False(result.IsDemoFiscal);
        Assert.Equal("RKSV-konform (Registrierkassensicherheitsverordnung)", result.RksvFooterLabel);
    }

    [Fact]
    public void Resolve_HidesDemoLabel_WhenShowDemoLabelFalseDespiteSimulatedTse()
    {
        var env = Mock.Of<IHostEnvironment>(h => h.EnvironmentName == Environments.Production);
        var rksv = new RksvOptions
        {
            Mode = "Production",
            TseMode = "Simulation",
            ShowDemoLabel = false
        };

        var result = FiscalEnvironmentResolver.Resolve(
            env,
            new TseOptions { Mode = "Fake", TseMode = "Demo" },
            rksvOptions: rksv,
            rksvEnvironment: CreateRksvEnvironment(new Dictionary<string, string?>
            {
                ["RKSV:Mode"] = "Production",
                ["RKSV:TseMode"] = "Simulation",
                ["RKSV:ShowDemoLabel"] = "false"
            }));

        Assert.True(result.IsDemoFiscal);
        Assert.Equal("RKSV-konform (Registrierkassensicherheitsverordnung)", result.RksvFooterLabel);
    }
}
