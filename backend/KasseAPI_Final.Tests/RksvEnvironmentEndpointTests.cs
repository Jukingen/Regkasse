using KasseAPI_Final.Controllers;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Rksv;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RksvEnvironmentEndpointTests
{
    private static RksvController CreateController(
        IRksvEnvironmentService rksvEnv,
        string hostEnvironment,
        TseOptions? tseOptions = null,
        IConfiguration? configuration = null)
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(hostEnvironment);
        var monitor = new Mock<IOptionsMonitor<TseOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(tseOptions ?? new TseOptions
        {
            TseMode = "Device",
            Mode = "Real",
            Provider = "fiskaly",
        });
        var config = configuration
                     ?? new ConfigurationBuilder()
                         .AddInMemoryCollection(new Dictionary<string, string?>
                         {
                             ["RKSV:Mode"] = "Production",
                             ["RKSV:TseMode"] = "Real",
                         })
                         .Build();
        return new RksvController(
            Mock.Of<IMonatsbelegReminderService>(),
            Mock.Of<IRksvReminderService>(),
            rksvEnv,
            env.Object,
            config,
            monitor.Object,
            Mock.Of<ICurrentTenantAccessor>());
    }

    [Fact]
    public void GetEnvironment_ReturnsDemoSnapshot_InDevelopmentHost()
    {
        var rksvEnv = new RksvEnvironmentService(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["RKSV:Mode"] = "Demo" })
                .Build(),
            Mock.Of<IHostEnvironment>(h => h.EnvironmentName == Environments.Development));

        var controller = CreateController(
            rksvEnv,
            Environments.Development,
            new TseOptions { TseMode = "Demo", Mode = "Fake" },
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["RKSV:Mode"] = "Demo" })
                .Build());

        var result = controller.GetEnvironment();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<RksvEnvironmentStatusDto>(ok.Value);
        Assert.Equal("Demo", dto.Environment);
        Assert.True(dto.IsSimulated);
        Assert.True(dto.IsHostDevelopment);
        Assert.True(dto.IsSimulationMode);
        Assert.Equal("dev", dto.ReleaseStage);
        Assert.False(dto.IsCanary);
        Assert.True(dto.FiscalConfigLockOk);
        Assert.Contains("SIMULIERT", dto.TseStatusDisplay, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetEnvironment_Production_Reports_Lock_Failure_When_Unsafe()
    {
        var rksvEnv = new RksvEnvironmentService(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RKSV:Mode"] = "Production",
                    ["RKSV:TseMode"] = "Real",
                })
                .Build(),
            Mock.Of<IHostEnvironment>(h => h.EnvironmentName == Environments.Production));

        var controller = CreateController(
            rksvEnv,
            Environments.Production,
            new TseOptions { TseMode = "Off", Mode = "Real", Provider = "fiskaly" });

        var result = controller.GetEnvironment();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<RksvEnvironmentStatusDto>(ok.Value);
        Assert.False(dto.FiscalConfigLockOk);
        Assert.NotEmpty(dto.FiscalConfigLockReasons);
    }

    [Fact]
    public void GetStatus_ReturnsSimulatedDemo_InDevelopmentHost()
    {
        var rksvEnv = new RksvEnvironmentService(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["RKSV:Mode"] = "Demo" })
                .Build(),
            Mock.Of<IHostEnvironment>(h => h.EnvironmentName == Environments.Development));

        var controller = CreateController(rksvEnv, Environments.Development);

        var result = controller.GetStatus();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<RksvStatusDto>(ok.Value);
        Assert.True(dto.IsSimulated);
        Assert.Equal("Demo", dto.Environment);
        Assert.True(dto.ShowDemoLabel);
        Assert.Contains("DEMO", dto.DisplayName, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SIMULIERT", dto.TseStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetStatus_ReturnsProduction_WhenConfiguredProduction()
    {
        var rksvEnv = new RksvEnvironmentService(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RKSV:Mode"] = "Production",
                    ["RKSV:TseMode"] = "Real",
                })
                .Build(),
            Mock.Of<IHostEnvironment>(h => h.EnvironmentName == Environments.Production));

        var controller = CreateController(rksvEnv, Environments.Production);

        var result = controller.GetStatus();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<RksvStatusDto>(ok.Value);
        Assert.False(dto.IsSimulated);
        Assert.Equal("Production", dto.Environment);
        Assert.Contains("AKTIV", dto.TseStatus, StringComparison.OrdinalIgnoreCase);
    }
}
