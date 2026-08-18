using System.Security.Claims;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Services.License;
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
        var controller = CreateController(
            Mock.Of<ITenantLicenseService>(),
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
    public async Task ActivateLicense_BillingKey_UsesBodyTenantIdWhenAmbientMissing()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var validUntil = DateTime.UtcNow.AddDays(365);
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
                ValidUntilUtc = validUntil,
                LicensePlan = "12_months",
            });

        var controller = CreateController(
            tenantLicenseService.Object,
            tenantId: null,
            userId,
            dbSeed: db =>
            {
                db.Tenants.Add(new Models.Tenant
                {
                    Id = tenantId,
                    Name = "Cafe",
                    Slug = "cafe",
                    Status = TenantStatuses.Active,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                });
            });

        var result = await controller.ActivateLicense(
            new ActivateLicenseRequest { LicenseKey = licenseKey, TenantId = tenantId },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<LicenseActivationResult>(ok.Value);
        Assert.True(payload.Success);
        Assert.Equal(tenantId, payload.TenantId);
        tenantLicenseService.Verify(
            x => x.ActivateLicenseAsync(tenantId, licenseKey, userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ActivateLicense_BillingKey_ActivatesViaTenantLicenseService()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var validUntil = DateTime.UtcNow.AddDays(365);
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
                ValidUntilUtc = validUntil,
                LicensePlan = "12_months",
            });

        var controller = CreateController(
            tenantLicenseService.Object,
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
        Assert.Equal("active", payload.Status);
        Assert.NotNull(payload.DaysRemaining);
        Assert.True(payload.DaysRemaining >= 364);
    }

    [Fact]
    public async Task ActivateLicense_SystemKey_UsesDeploymentLicenseService()
    {
        const string licenseKey = "REGK-20261231-system-C8YEM41L";
        var tenantLicenseService = new Mock<ITenantLicenseService>(MockBehavior.Strict);
        var licenseService = new Mock<ILicenseService>();
        licenseService
            .Setup(x => x.ActivateAsync(
                It.Is<ActivateLicenseRequest>(r => r.LicenseKey == licenseKey),
                It.IsAny<LicenseActivationClientInfo?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LicenseActivationResult(true, "Lizenz erfolgreich aktiviert", DateTime.UtcNow.AddDays(30), "Licensed"));

        var controller = CreateController(
            tenantLicenseService.Object,
            tenantId: null,
            licenseService: licenseService.Object,
            dbSeed: db =>
            {
                db.IssuedLicenses.Add(new Models.IssuedLicense
                {
                    LicenseKey = licenseKey,
                    CustomerName = "Server",
                    ExpiryAtUtc = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc),
                    SignedJwt = "jwt",
                    IssuedAtUtc = DateTime.UtcNow,
                });
            });

        var result = await controller.ActivateLicense(
            new ActivateLicenseRequest { LicenseKey = licenseKey },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<LicenseActivationResult>(ok.Value);
        Assert.True(payload.Success);
        tenantLicenseService.Verify(
            x => x.ActivateLicenseAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ActivateLicense_DevTenantKey_SucceedsForDevTenant()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string licenseKey = "REGK-20261231-dev-A4WCG52H";
        var validUntil = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc);

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
            tenantId,
            userId,
            dbSeed: db =>
            {
                db.Tenants.Add(new Tenant
                {
                    Id = tenantId,
                    Name = "Dev",
                    Slug = "dev",
                    Status = TenantStatuses.Active,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                });
                db.LicenseSales.Add(new LicenseSale
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    LicenseKey = licenseKey,
                    LicensePlan = "12_months",
                    ValidFromUtc = DateTime.UtcNow.AddDays(-1),
                    ValidUntilUtc = validUntil,
                    Status = LicenseSaleStatuses.Active,
                    SoldAtUtc = DateTime.UtcNow,
                    SoldByUserId = userId,
                    InvoiceNumber = "INV-DEV",
                    CreatedAt = DateTime.UtcNow,
                });
            });

        var result = await controller.ActivateLicense(
            new ActivateLicenseRequest { LicenseKey = licenseKey },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(Assert.IsType<LicenseActivationResult>(ok.Value).Success);
    }

    [Fact]
    public async Task ActivateLicense_DevKeyOnProdTenant_ReturnsSlugMismatch()
    {
        var devId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string licenseKey = "REGK-20261231-dev-A4WCG52H";
        var validUntil = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        var tenantLicenseService = new Mock<ITenantLicenseService>(MockBehavior.Strict);

        var controller = CreateController(
            tenantLicenseService.Object,
            prodId,
            userId,
            dbSeed: db =>
            {
                db.Tenants.AddRange(
                    new Tenant
                    {
                        Id = devId,
                        Name = "Dev",
                        Slug = "dev",
                        Status = TenantStatuses.Active,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                    },
                    new Tenant
                    {
                        Id = prodId,
                        Name = "Prod",
                        Slug = "prod",
                        Status = TenantStatuses.Active,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                    });
                db.LicenseSales.Add(new LicenseSale
                {
                    Id = Guid.NewGuid(),
                    TenantId = devId,
                    LicenseKey = licenseKey,
                    LicensePlan = "12_months",
                    ValidFromUtc = DateTime.UtcNow.AddDays(-1),
                    ValidUntilUtc = validUntil,
                    Status = LicenseSaleStatuses.Active,
                    SoldAtUtc = DateTime.UtcNow,
                    SoldByUserId = userId,
                    InvoiceNumber = "INV-DEV",
                    CreatedAt = DateTime.UtcNow,
                });
            });

        var result = await controller.ActivateLicense(
            new ActivateLicenseRequest { LicenseKey = licenseKey },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var payload = Assert.IsType<LicenseActivationResult>(badRequest.Value);
        Assert.False(payload.Success);
        Assert.Contains("anderen Mandanten", payload.Message, StringComparison.OrdinalIgnoreCase);
        tenantLicenseService.Verify(
            x => x.ActivateLicenseAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ActivateLicense_ExpiredKey_ReturnsExpired()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string licenseKey = "REGK-20250101-dev-XXXXXXXX";
        var tenantLicenseService = new Mock<ITenantLicenseService>(MockBehavior.Strict);

        var controller = CreateController(
            tenantLicenseService.Object,
            tenantId,
            userId,
            dbSeed: db =>
            {
                db.Tenants.Add(new Tenant
                {
                    Id = tenantId,
                    Name = "Dev",
                    Slug = "dev",
                    Status = TenantStatuses.Active,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                });
            });

        var result = await controller.ActivateLicense(
            new ActivateLicenseRequest { LicenseKey = licenseKey },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var payload = Assert.IsType<LicenseActivationResult>(badRequest.Value);
        Assert.False(payload.Success);
        Assert.Contains("expired", payload.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ActivateLicense_MappedLegacyKey_RoutesToBilling()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string oldKey = "REGK-A4WCG-52HL9-66AQI";
        const string newKey = "REGK-20270101-dev-1R61EMER";
        var validUntil = DateTime.UtcNow.AddDays(365);

        var tenantLicenseService = new Mock<ITenantLicenseService>();
        tenantLicenseService
            .Setup(x => x.ActivateLicenseAsync(
                tenantId,
                newKey,
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivationResult
            {
                Success = true,
                Message = "Lizenz wurde erfolgreich aktiviert.",
                LicenseKey = newKey,
                ValidUntilUtc = validUntil,
                LicensePlan = "12_months",
            });

        var controller = CreateController(
            tenantLicenseService.Object,
            tenantId,
            userId,
            dbSeed: db =>
            {
                db.LicenseKeyMappings.Add(new Models.LicenseKeyMapping
                {
                    Id = Guid.NewGuid(),
                    OldLicenseKey = oldKey,
                    NewLicenseKey = newKey,
                    LicenseKind = Models.LicenseKeyKinds.Tenant,
                    SourceTable = "license_sales",
                    CreatedAtUtc = DateTime.UtcNow,
                });
            });

        var result = await controller.ActivateLicense(
            new ActivateLicenseRequest { LicenseKey = oldKey },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(Assert.IsType<LicenseActivationResult>(ok.Value).Success);
        tenantLicenseService.Verify(
            x => x.ActivateLicenseAsync(tenantId, newKey, userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidateLicense_ReturnsUnifiedValidationResult()
    {
        var controller = CreateController(Mock.Of<ITenantLicenseService>(), tenantId: null);

        var result = await controller.ValidateLicense(
            new LicenseKeyLookupRequest { LicenseKey = "not-a-key" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<LicenseKeyValidationResult>(ok.Value);
        Assert.False(payload.IsValid);
        Assert.False(payload.IsFormatValid);
        Assert.Equal(LicenseKeyErrorCodes.InvalidFormat, payload.ErrorCode);
    }

    [Fact]
    public async Task GetLicenseInfo_WithoutKey_ReturnsBadRequest()
    {
        var controller = CreateController(Mock.Of<ITenantLicenseService>(), tenantId: null);

        var result = await controller.GetLicenseInfo(null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetLicenseInfo_SystemKey_ReturnsKindAndExpiry()
    {
        const string key = "REGK-20990101-system-INFOKEY1";
        var controller = CreateController(
            Mock.Of<ITenantLicenseService>(),
            tenantId: null,
            dbSeed: db =>
            {
                db.IssuedLicenses.Add(new Models.IssuedLicense
                {
                    LicenseKey = key,
                    CustomerName = "Acme GmbH",
                    ExpiryAtUtc = new DateTime(2099, 1, 1, 23, 59, 59, DateTimeKind.Utc),
                    SignedJwt = "jwt",
                    IssuedAtUtc = DateTime.UtcNow,
                });
            });

        var result = await controller.GetLicenseInfo(key, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var info = Assert.IsType<LicenseInfo>(ok.Value);
        Assert.True(info.Exists);
        Assert.Equal(LicenseKeyKinds.System, info.LicenseKind);
        Assert.Equal("Acme GmbH", info.CustomerName);
    }

    private static LicenseController CreateController(
        ITenantLicenseService tenantLicenseService,
        Guid? tenantId,
        Guid? userId = null,
        ILicenseService? licenseService = null,
        Action<AppDbContext>? dbSeed = null)
    {
        var tenantAccessor = new Mock<ICurrentTenantAccessor>();
        tenantAccessor.Setup(x => x.TenantId).Returns(tenantId);

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"LicenseActivate_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AppDbContext(dbOptions, NullCurrentTenantAccessor.Instance);
        dbSeed?.Invoke(db);
        if (dbSeed != null)
            db.SaveChanges();

        var deployment = licenseService ?? Mock.Of<ILicenseService>();
        var unified = new UnifiedLicenseService(
            db,
            deployment,
            tenantLicenseService,
            Mock.Of<ILicenseStatusCache>(),
            tenantAccessor.Object,
            NullLogger<UnifiedLicenseService>.Instance);

        var controller = new LicenseController(
            deployment,
            tenantLicenseService,
            unified,
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
