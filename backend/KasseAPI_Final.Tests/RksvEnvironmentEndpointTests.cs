using KasseAPI_Final.Controllers;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Rksv;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RksvEnvironmentEndpointTests
{
    [Fact]
    public void GetEnvironment_ReturnsDemoSnapshot_InDevelopmentHost()
    {
        var rksvEnv = new RksvEnvironmentService(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["RKSV:Mode"] = "Demo" })
                .Build(),
            Mock.Of<IHostEnvironment>(h => h.EnvironmentName == Environments.Development));

        var controller = new RksvController(
            Mock.Of<IMonatsbelegReminderService>(),
            Mock.Of<IRksvReminderService>(),
            rksvEnv);

        var result = controller.GetEnvironment();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<RksvEnvironmentStatusDto>(ok.Value);
        Assert.Equal("Demo", dto.Environment);
        Assert.True(dto.IsSimulated);
        Assert.Contains("SIMULIERT", dto.TseStatusDisplay, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetStatus_ReturnsSimulatedDemo_InDevelopmentHost()
    {
        var rksvEnv = new RksvEnvironmentService(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["RKSV:Mode"] = "Demo" })
                .Build(),
            Mock.Of<IHostEnvironment>(h => h.EnvironmentName == Environments.Development));

        var controller = new RksvController(
            Mock.Of<IMonatsbelegReminderService>(),
            Mock.Of<IRksvReminderService>(),
            rksvEnv);

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

        var controller = new RksvController(
            Mock.Of<IMonatsbelegReminderService>(),
            Mock.Of<IRksvReminderService>(),
            rksvEnv);

        var result = controller.GetStatus();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<RksvStatusDto>(ok.Value);
        Assert.False(dto.IsSimulated);
        Assert.Equal("Production", dto.Environment);
        Assert.Contains("AKTIV", dto.TseStatus, StringComparison.OrdinalIgnoreCase);
    }
}
