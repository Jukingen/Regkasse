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

public sealed class LicenseControllerActivateTests
{
    [Fact]
    public async Task ActivateLicense_BillingKeyWithoutTenant_ReturnsBadRequest()
    {
        var keyGenerator = new Mock<ILicenseKeyGenerator>();
        keyGenerator.Setup(x => x.ValidateLicenseKeyFormat(It.IsAny<string>())).Returns(true);

        var controller = CreateController(
            Mock.Of<ITenantLicenseService>(),
            keyGenerator.Object,
            tenantId: null);

        var result = await controller.ActivateLicense(
            new ActivateLicenseRequest { LicenseKey = "REGK-20270101-cafe-A7F3K2D9" },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var payload = Assert.IsType<LicenseActivationResult>(badRequest.Value);
        Assert.False(payload.Success);
        Assert.Equal("Tenant context required.", payload.Message);
    }

    [Fact]
    public async Task ActivateLicense_BillingKey_ActivatesViaTenantLicenseService()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var validUntil = DateTime.UtcNow.AddDays(365);
        const string licenseKey = "REGK-20270101-cafe-A7F3K2D9";

        var keyGenerator = new Mock<ILicenseKeyGenerator>();
        keyGenerator.Setup(x => x.ValidateLicenseKeyFormat(licenseKey)).Returns(true);

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
                ValidUntilUtc = validUntil,
                LicensePlan = "12_months",
            });

        var controller = CreateController(
            tenantLicenseService.Object,
            keyGenerator.Object,
            tenantId,
            userId);

        var result = await controller.ActivateLicense(
            new ActivateLicenseRequest { LicenseKey = licenseKey },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<LicenseActivationResult>(ok.Value);
        Assert.True(payload.Success);
        Assert.Equal("Lizenz wurde erfolgreich aktiviert.", payload.Message);
        Assert.Equal(validUntil, payload.ValidUntil);
        Assert.Equal("12_months", payload.LicenseType);
    }

    private static LicenseController CreateController(
        ITenantLicenseService tenantLicenseService,
        ILicenseKeyGenerator licenseKeyGenerator,
        Guid? tenantId,
        Guid? userId = null)
    {
        var tenantAccessor = new Mock<ICurrentTenantAccessor>();
        tenantAccessor.Setup(x => x.TenantId).Returns(tenantId);

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"LicenseActivate_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AppDbContext(dbOptions, NullCurrentTenantAccessor.Instance);

        var controller = new LicenseController(
            Mock.Of<ILicenseService>(),
            tenantLicenseService,
            licenseKeyGenerator,
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
