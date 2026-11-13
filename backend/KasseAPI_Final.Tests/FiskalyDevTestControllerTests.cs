using KasseAPI_Final.Controllers;
using KasseAPI_Final.Tse.Fiskaly;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiskalyDevTestControllerTests
{
    [Fact]
    public async Task ProbeConnection_OutsideDevelopment_ReturnsNotFound()
    {
        var probe = new Mock<IFiskalyConnectionProbe>(MockBehavior.Strict);
        var controller = new FiskalyDevTestController(
            new FakeWebHostEnvironment(Environments.Production),
            probe.Object,
            NullLogger<FiskalyDevTestController>.Instance);

        var result = await controller.ProbeConnectionAsync(null, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
        probe.Verify(
            p => p.ProbeAsync(It.IsAny<FiskalyConnectionProbeRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProbeConnection_InDevelopment_ReturnsProbeResult()
    {
        var expected = new FiskalyConnectionProbeResult
        {
            Success = true,
            Authentication = new FiskalyConnectionStepResult
            {
                Name = "Authentication",
                Status = "Succeeded",
                HttpStatus = 200,
                Message = "Token received."
            },
            ApiBaseUrl = "https://rksv.fiskaly.com/api/v1"
        };
        var probe = new Mock<IFiskalyConnectionProbe>();
        probe
            .Setup(p => p.ProbeAsync(It.IsAny<FiskalyConnectionProbeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = new FiskalyDevTestController(
            new FakeWebHostEnvironment(Environments.Development),
            probe.Object,
            NullLogger<FiskalyDevTestController>.Instance);

        var result = await controller.ProbeConnectionAsync(
            new FiskalyConnectionProbeRequest { CreateResources = false },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<FiskalyConnectionProbeResult>(ok.Value);
        Assert.True(dto.Success);
        Assert.Equal("Succeeded", dto.Authentication.Status);
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public FakeWebHostEnvironment(string name) => EnvironmentName = name;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = ".";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
