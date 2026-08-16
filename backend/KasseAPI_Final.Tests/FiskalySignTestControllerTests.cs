using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Tse.Fiskaly;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiskalySignTestControllerTests
{
    [Fact]
    public void GetScenarios_OutsideDevelopment_ReturnsNotFound()
    {
        var service = new Mock<IFiskalySignTestService>(MockBehavior.Strict);
        var controller = new FiskalySignTestController(
            new FakeWebHostEnvironment(Environments.Production),
            service.Object);

        var result = controller.GetScenarios();

        Assert.IsType<NotFoundResult>(result.Result);
        service.Verify(s => s.GetScenarios(), Times.Never);
    }

    [Fact]
    public async Task SignTest_OutsideDevelopment_ReturnsNotFound()
    {
        var service = new Mock<IFiskalySignTestService>(MockBehavior.Strict);
        var controller = new FiskalySignTestController(
            new FakeWebHostEnvironment(Environments.Staging),
            service.Object);

        var result = await controller.SignTest(
            new FiskalySignTestRequest { CashRegisterId = Guid.NewGuid() },
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task SignTest_InDevelopment_ReturnsOk()
    {
        var registerId = Guid.NewGuid();
        var expected = new FiskalySignTestResultDto
        {
            Success = true,
            Scenario = FiskalySignTestScenarioIds.Normal,
            ReceiptId = Guid.NewGuid().ToString("D"),
            ReceiptNumber = "42",
            Signed = true
        };
        var service = new Mock<IFiskalySignTestService>();
        service
            .Setup(s => s.SignAsync(
                It.IsAny<FiskalySignTestRequest>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(FiskalySetupOperationResult<FiskalySignTestResultDto>.Ok(expected));

        var controller = new FiskalySignTestController(
            new FakeWebHostEnvironment(Environments.Development),
            service.Object);
        AttachSuperAdmin(controller);

        var result = await controller.SignTest(
            new FiskalySignTestRequest { CashRegisterId = registerId, Scenario = "normal" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<FiskalySignTestResultDto>(ok.Value);
        Assert.True(dto.Success);
        Assert.Equal("42", dto.ReceiptNumber);
    }

    [Fact]
    public async Task VerifyTest_InDevelopment_MapsNotFound()
    {
        var service = new Mock<IFiskalySignTestService>();
        service
            .Setup(s => s.VerifyAsync(
                It.IsAny<FiskalyVerifyTestRequest>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(FiskalySetupOperationResult<FiskalyVerifyTestResultDto>.Fail(404, "Cash register not found."));

        var controller = new FiskalySignTestController(
            new FakeWebHostEnvironment(Environments.Development),
            service.Object);
        AttachSuperAdmin(controller);

        var result = await controller.VerifyTest(
            new FiskalyVerifyTestRequest { CashRegisterId = Guid.NewGuid(), ReceiptId = "42" },
            CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.NotNull(notFound.Value);
    }

    private static void AttachSuperAdmin(ControllerBase controller)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "sa-1"),
                new Claim(ClaimTypes.Role, Roles.SuperAdmin)
            ],
            "Test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
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
