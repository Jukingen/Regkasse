using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Time;
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
    public async Task SignTest_Tagesabschluss_DelegatesToReceiptService()
    {
        var registerId = Guid.NewGuid();
        var signTest = new Mock<IFiskalySignTestService>(MockBehavior.Strict);
        var receipts = new Mock<IFiskalyReceiptService>();
        receipts
            .Setup(s => s.CreateTagesabschlussAsync(
                registerId,
                It.IsAny<DateTime?>(),
                "sa-1",
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(FiskalyReceiptOperationResult.Ok(new FiskalyReceiptDataDto
            {
                ReceiptId = "fiskaly-ta",
                ReceiptNumber = "TA-20260829",
                QrCode = "R1-AT3_ta"
            }));

        var controller = new FiskalySignTestController(
            new FakeWebHostEnvironment(Environments.Development),
            signTest.Object,
            receipts.Object);
        AttachSuperAdmin(controller);

        var result = await controller.SignTest(
            new FiskalySignTestRequest
            {
                CashRegisterId = registerId,
                Scenario = FiskalySignTestScenarioIds.Tagesabschluss
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<FiskalySignTestResultDto>(ok.Value);
        Assert.True(dto.Success);
        Assert.Equal(FiskalySignTestScenarioIds.Tagesabschluss, dto.Scenario);
        Assert.Equal("fiskaly-ta", dto.ReceiptId);
        Assert.Equal("NORMAL", dto.ReceiptType);
        signTest.Verify(
            s => s.SignAsync(
                It.IsAny<FiskalySignTestRequest>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SignTest_Tagesabschluss_MissingClosing_ReturnsNotFound()
    {
        var signTest = new Mock<IFiskalySignTestService>(MockBehavior.Strict);
        var receipts = new Mock<IFiskalyReceiptService>();
        receipts
            .Setup(s => s.CreateTagesabschlussAsync(
                It.IsAny<Guid>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(FiskalyReceiptOperationResult.Fail(
                404,
                FiskalyReceiptErrorCodes.ClosingNotFound,
                "Daily closing not found."));

        var controller = new FiskalySignTestController(
            new FakeWebHostEnvironment(Environments.Development),
            signTest.Object,
            receipts.Object);
        AttachSuperAdmin(controller);

        var result = await controller.SignTest(
            new FiskalySignTestRequest
            {
                CashRegisterId = Guid.NewGuid(),
                Scenario = FiskalySignTestScenarioIds.Tagesabschluss
            },
            CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task SignTest_MonthlyClose_DelegatesToReceiptService()
    {
        var registerId = Guid.NewGuid();
        var signTest = new Mock<IFiskalySignTestService>(MockBehavior.Strict);
        var receipts = new Mock<IFiskalyReceiptService>();
        receipts
            .Setup(s => s.CreateMonatsbelegAsync(
                registerId,
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                "sa-1",
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(FiskalyReceiptOperationResult.Ok(new FiskalyReceiptDataDto
            {
                ReceiptId = "mb-1",
                ReceiptNumber = "AT-MB-1",
                QrCode = "R1-AT3_mb"
            }));

        var controller = new FiskalySignTestController(
            new FakeWebHostEnvironment(Environments.Development),
            signTest.Object,
            receipts.Object);
        AttachSuperAdmin(controller);

        var (year, month) = PostgreSqlUtcDateTime.GetViennaPreviousYearMonth();
        var result = await controller.SignTest(
            new FiskalySignTestRequest
            {
                CashRegisterId = registerId,
                Scenario = FiskalySignTestScenarioIds.MonthlyClose,
                Year = year,
                Month = month
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<FiskalySignTestResultDto>(ok.Value);
        Assert.True(dto.Success);
        Assert.Equal("mb-1", dto.ReceiptId);
        signTest.Verify(
            s => s.SignAsync(
                It.IsAny<FiskalySignTestRequest>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SignTest_MonthlyClose_MissingStartbeleg_ReturnsBadRequestEnvelope()
    {
        var receipts = new Mock<IFiskalyReceiptService>();
        receipts
            .Setup(s => s.CreateMonatsbelegAsync(
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(FiskalyReceiptOperationResult.Fail(
                400,
                FiskalyReceiptErrorCodes.StartbelegRequired,
                "Startbeleg is required before creating Monatsbeleg or Jahresbeleg."));

        var controller = new FiskalySignTestController(
            new FakeWebHostEnvironment(Environments.Development),
            new Mock<IFiskalySignTestService>(MockBehavior.Strict).Object,
            receipts.Object);
        AttachSuperAdmin(controller);

        var (year, month) = PostgreSqlUtcDateTime.GetViennaPreviousYearMonth();
        var result = await controller.SignTest(
            new FiskalySignTestRequest
            {
                CashRegisterId = Guid.NewGuid(),
                Scenario = FiskalySignTestScenarioIds.MonthlyClose,
                Year = year,
                Month = month
            },
            CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.NotNull(bad.Value);
    }

    [Fact]
    public async Task SignTest_MonthlyClose_CurrentMonth_ReturnsPeriodNotCompleted()
    {
        var (year, month) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();
        var receipts = new Mock<IFiskalyReceiptService>(MockBehavior.Strict);
        var controller = new FiskalySignTestController(
            new FakeWebHostEnvironment(Environments.Development),
            new Mock<IFiskalySignTestService>(MockBehavior.Strict).Object,
            receipts.Object);
        AttachSuperAdmin(controller);

        var result = await controller.SignTest(
            new FiskalySignTestRequest
            {
                CashRegisterId = Guid.NewGuid(),
                Scenario = FiskalySignTestScenarioIds.MonthlyClose,
                Year = year,
                Month = month
            },
            CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.NotNull(bad.Value);
        receipts.Verify(
            s => s.CreateMonatsbelegAsync(
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
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
