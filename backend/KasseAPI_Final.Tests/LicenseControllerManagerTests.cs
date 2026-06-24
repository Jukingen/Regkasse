using System.Security.Claims;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LicenseControllerManagerTests
{
    [Fact]
    public async Task GetBillingStatus_WithoutTenant_ReturnsBadRequest()
    {
        var controller = CreateController(
            Mock.Of<ITenantLicenseService>(),
            tenantId: null);

        var result = await controller.GetBillingStatus(CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task GetBillingStatus_WithTenant_ReturnsStatus()
    {
        var tenantId = Guid.NewGuid();
        var expected = new TenantLicenseStatus
        {
            IsValid = true,
            Status = "valid",
            DaysRemaining = 30,
            LicenseKey = "REGK-20270101-cafe-A7F3K2D9",
        };

        var tenantLicenseService = new Mock<ITenantLicenseService>();
        tenantLicenseService
            .Setup(x => x.GetCurrentStatusAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = CreateController(tenantLicenseService.Object, tenantId, userId: Guid.NewGuid());
        var result = await controller.GetBillingStatus(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<TenantLicenseStatus>(ok.Value);
        Assert.True(payload.IsValid);
        Assert.Equal(30, payload.DaysRemaining);
    }

    [Fact]
    public async Task ExtendLicense_Success_ReturnsExtendResult()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string licenseKey = "REGK-20270101-cafe-A7F3K2D9";

        var tenantLicenseService = new Mock<ITenantLicenseService>();
        tenantLicenseService
            .Setup(x => x.ExtendLicenseAsync(
                tenantId,
                licenseKey,
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtendResult
            {
                Success = true,
                Message = "Lizenz wurde erfolgreich verlängert.",
                LicenseKey = licenseKey,
            });

        var controller = CreateController(tenantLicenseService.Object, tenantId, userId);
        var result = await controller.ExtendLicense(
            new ExtendLicenseRequest { LicenseKey = licenseKey },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ExtendResult>(ok.Value);
        Assert.True(payload.Success);
    }

    [Fact]
    public async Task ExtendLicense_Failure_ReturnsBadRequest()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var tenantLicenseService = new Mock<ITenantLicenseService>();
        tenantLicenseService
            .Setup(x => x.ExtendLicenseAsync(
                tenantId,
                It.IsAny<string>(),
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtendResult
            {
                Success = false,
                Message = "Lizenzschlüssel nicht gefunden.",
            });

        var controller = CreateController(tenantLicenseService.Object, tenantId, userId);
        var result = await controller.ExtendLicense(
            new ExtendLicenseRequest { LicenseKey = "REGK-20270101-cafe-INVALID1" },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task ActivateBillingLicense_Success_ReturnsActivationResult()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string licenseKey = "REGK-20270101-cafe-A7F3K2D9";

        var tenantLicenseService = new Mock<ITenantLicenseService>();
        tenantLicenseService
            .Setup(x => x.ActivateLicenseAsync(
                tenantId,
                licenseKey,
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivationResult
            {
                Success = true,
                Message = "Lizenz wurde erfolgreich aktiviert.",
                LicenseKey = licenseKey,
            });

        var controller = CreateController(tenantLicenseService.Object, tenantId, userId);
        var result = await controller.ActivateBillingLicense(
            new MandantLicenseKeyRequest { LicenseKey = licenseKey },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ActivationResult>(ok.Value);
        Assert.True(payload.Success);
    }

    private static LicenseController CreateController(
        ITenantLicenseService tenantLicenseService,
        Guid? tenantId,
        Guid? userId = null)
    {
        var tenantAccessor = new Mock<ICurrentTenantAccessor>();
        tenantAccessor.Setup(x => x.TenantId).Returns(tenantId);

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"LicenseManager_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AppDbContext(dbOptions, NullCurrentTenantAccessor.Instance);

        var controller = new LicenseController(
            Mock.Of<ILicenseService>(),
            tenantLicenseService,
            Mock.Of<ILicenseKeyGenerator>(),
            Options.Create(new Configuration.LicenseOptions()),
            Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>(),
            NullLogger<LicenseController>.Instance,
            tenantAccessor.Object,
            db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };

        if (userId.HasValue)
        {
            controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString("D")),
                ],
                authenticationType: "Test"));
        }

        return controller;
    }
}
