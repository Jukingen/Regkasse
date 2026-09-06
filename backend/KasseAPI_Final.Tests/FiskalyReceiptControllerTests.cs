using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiskalyReceiptControllerTests
{
    [Fact]
    public async Task Normal_NullBody_ReturnsStructuredValidationError()
    {
        var service = new Mock<IFiskalyReceiptService>(MockBehavior.Strict);
        var controller = CreateController(service.Object);

        var result = await controller.CreateNormal(null, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var envelope = Assert.IsType<FiskalyReceiptEnvelopeDto>(bad.Value);
        Assert.False(envelope.Success);
        Assert.Equal(FiskalyReceiptErrorCodes.ValidationError, envelope.Error?.Code);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Normal_Success_ReturnsEnvelope()
    {
        var registerId = Guid.NewGuid();
        var service = new Mock<IFiskalyReceiptService>();
        service
            .Setup(s => s.CreateNormalReceiptAsync(
                registerId,
                10.00m,
                "STANDARD",
                It.IsAny<string>(),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(FiskalyReceiptOperationResult.Ok(new FiskalyReceiptDataDto
            {
                ReceiptId = "abc-123",
                ReceiptNumber = "42",
                QrCode = "R1-AT3_test"
            }));

        var controller = CreateController(service.Object);
        var result = await controller.CreateNormal(
            new FiskalyNormalReceiptRequest
            {
                CashRegisterId = registerId,
                Amount = 10.00m,
                VatRate = "STANDARD"
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var envelope = Assert.IsType<FiskalyReceiptEnvelopeDto>(ok.Value);
        Assert.True(envelope.Success);
        Assert.Equal("42", envelope.Data?.ReceiptNumber);
        Assert.Equal("R1-AT3_test", envelope.Data?.QrCode);
        Assert.Null(envelope.HistoryId);
    }

    [Fact]
    public async Task Normal_Success_CopiesHistoryId()
    {
        var registerId = Guid.NewGuid();
        var historyId = Guid.NewGuid();
        var service = new Mock<IFiskalyReceiptService>();
        service
            .Setup(s => s.CreateNormalReceiptAsync(
                registerId,
                10.00m,
                "STANDARD",
                It.IsAny<string>(),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FiskalyReceiptOperationResult
            {
                Success = true,
                StatusCode = 200,
                Data = new FiskalyReceiptDataDto { ReceiptId = "abc-123", ReceiptNumber = "42" },
                HistoryId = historyId
            });

        var controller = CreateController(service.Object);
        var result = await controller.CreateNormal(
            new FiskalyNormalReceiptRequest
            {
                CashRegisterId = registerId,
                Amount = 10.00m,
                VatRate = "STANDARD"
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var envelope = Assert.IsType<FiskalyReceiptEnvelopeDto>(ok.Value);
        Assert.Equal(historyId, envelope.HistoryId);
    }

    [Fact]
    public async Task Cancel_NotFound_Maps404Envelope()
    {
        var service = new Mock<IFiskalyReceiptService>();
        service
            .Setup(s => s.CancelReceiptAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(FiskalyReceiptOperationResult.Fail(
                404,
                FiskalyReceiptErrorCodes.PaymentNotFound,
                "Payment not found."));

        var controller = CreateController(service.Object);
        var result = await controller.Cancel(
            new FiskalyCancelReceiptRequest
            {
                CashRegisterId = Guid.NewGuid(),
                OriginalReceiptId = Guid.NewGuid(),
                Reason = "Kundenwunsch Storno"
            },
            CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var envelope = Assert.IsType<FiskalyReceiptEnvelopeDto>(notFound.Value);
        Assert.False(envelope.Success);
        Assert.Equal(FiskalyReceiptErrorCodes.PaymentNotFound, envelope.Error?.Code);
        Assert.Equal("Payment not found.", envelope.Error?.Message);
    }

    [Fact]
    public void Endpoints_RequireFiskalyOperationPermissions_NotSuperAdminRole()
    {
        var type = typeof(FiskalyReceiptController);
        var classAuthorize = type.GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToList();
        Assert.DoesNotContain(classAuthorize, a => a.Roles == Roles.SuperAdmin);

        Assert.Contains(
            AppPermissions.FiskalyOperationsNormal,
            type.GetMethod(nameof(FiskalyReceiptController.CreateNormal))!
                .GetCustomAttributes<HasPermissionAttribute>()
                .Select(a => a.Permission));
        Assert.Contains(
            AppPermissions.FiskalyOperationsCancel,
            type.GetMethod(nameof(FiskalyReceiptController.Cancel))!
                .GetCustomAttributes<HasPermissionAttribute>()
                .Select(a => a.Permission));
        Assert.Contains(
            AppPermissions.FiskalyOperationsNullbeleg,
            type.GetMethod(nameof(FiskalyReceiptController.CreateNullbeleg))!
                .GetCustomAttributes<HasPermissionAttribute>()
                .Select(a => a.Permission));
        Assert.Contains(
            AppPermissions.FiskalyOperationsStartbeleg,
            type.GetMethod(nameof(FiskalyReceiptController.CreateStartbeleg))!
                .GetCustomAttributes<HasPermissionAttribute>()
                .Select(a => a.Permission));
        Assert.Contains(
            AppPermissions.FiskalyOperationsMonatsbeleg,
            type.GetMethod(nameof(FiskalyReceiptController.CreateMonatsbeleg))!
                .GetCustomAttributes<HasPermissionAttribute>()
                .Select(a => a.Permission));
        Assert.Contains(
            AppPermissions.FiskalyOperationsJahresbeleg,
            type.GetMethod(nameof(FiskalyReceiptController.CreateJahresbeleg))!
                .GetCustomAttributes<HasPermissionAttribute>()
                .Select(a => a.Permission));
        Assert.Contains(
            AppPermissions.FiskalyOperationsSchlussbeleg,
            type.GetMethod(nameof(FiskalyReceiptController.CreateSchlussbeleg))!
                .GetCustomAttributes<HasPermissionAttribute>()
                .Select(a => a.Permission));
        Assert.Contains(
            AppPermissions.FiskalyOperationsTagesabschluss,
            type.GetMethod(nameof(FiskalyReceiptController.CreateTagesabschluss))!
                .GetCustomAttributes<HasPermissionAttribute>()
                .Select(a => a.Permission));
        Assert.Contains(
            AppPermissions.FiskalyOperationsTagesabschluss,
            type.GetMethod(nameof(FiskalyReceiptController.CreateTagesabschlussForRegister))!
                .GetCustomAttributes<HasPermissionAttribute>()
                .Select(a => a.Permission));
    }

    [Fact]
    public async Task Nullbeleg_FiskalyAuthFailed_Returns400Envelope()
    {
        var service = new Mock<IFiskalyReceiptService>();
        service
            .Setup(s => s.CreateNullbelegAsync(
                It.IsAny<Guid>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(FiskalyReceiptOperationResult.Fail(
                400,
                FiskalyReceiptErrorCodes.FiskalyAuthFailed,
                "Fiskaly authentication failed",
                "Invalid API key or secret"));

        var controller = CreateController(service.Object);
        var result = await controller.CreateNullbeleg(
            new FiskalyNullbelegReceiptRequest { CashRegisterId = Guid.NewGuid() },
            CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var envelope = Assert.IsType<FiskalyReceiptEnvelopeDto>(bad.Value);
        Assert.False(envelope.Success);
        Assert.Equal(FiskalyReceiptErrorCodes.FiskalyAuthFailed, envelope.Error?.Code);
        Assert.Equal("Invalid API key or secret", envelope.Error?.Details);
    }

    [Fact]
    public async Task Tagesabschluss_Success_ReturnsEnvelope()
    {
        var closingId = Guid.NewGuid();
        var service = new Mock<IFiskalyReceiptService>();
        service
            .Setup(s => s.CreateTagesabschlussAsync(
                closingId,
                It.IsAny<string>(),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(FiskalyReceiptOperationResult.Ok(new FiskalyReceiptDataDto
            {
                ReceiptId = "fiskaly-ta",
                ReceiptNumber = "TA-20260829",
                Signature = "local-jws"
            }));

        var controller = CreateController(service.Object);
        var result = await controller.CreateTagesabschluss(closingId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var envelope = Assert.IsType<FiskalyReceiptEnvelopeDto>(ok.Value);
        Assert.True(envelope.Success);
        Assert.Equal("fiskaly-ta", envelope.Data?.ReceiptId);
        Assert.Equal("local-jws", envelope.Data?.Signature);
    }

    [Fact]
    public async Task TagesabschlussForRegister_Success_ReturnsEnvelope()
    {
        var registerId = Guid.NewGuid();
        var service = new Mock<IFiskalyReceiptService>();
        service
            .Setup(s => s.CreateTagesabschlussAsync(
                registerId,
                It.IsAny<DateTime?>(),
                It.IsAny<string>(),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(FiskalyReceiptOperationResult.Ok(new FiskalyReceiptDataDto
            {
                ReceiptId = "fiskaly-ta-reg",
                ReceiptNumber = "TA-20260829",
                Signature = "local-jws"
            }));

        var controller = CreateController(service.Object);
        var result = await controller.CreateTagesabschlussForRegister(
            new FiskalyTagesabschlussReceiptRequest { CashRegisterId = registerId },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var envelope = Assert.IsType<FiskalyReceiptEnvelopeDto>(ok.Value);
        Assert.True(envelope.Success);
        Assert.Equal("fiskaly-ta-reg", envelope.Data?.ReceiptId);
    }

    private static FiskalyReceiptController CreateController(IFiskalyReceiptService service)
    {
        var controller = new FiskalyReceiptController(service);
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
        return controller;
    }
}
