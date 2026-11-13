using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Services.License;
using KasseAPI_Final.Services.Trial;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using IAdminTenantLicenseKeyService = KasseAPI_Final.Services.AdminTenants.ITenantLicenseService;

namespace KasseAPI_Final.Tests;

public sealed class AdminTenantLicensesControllerTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TenantLicCtrl_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    [Fact]
    public async Task Put_Manager_CrossTenant_Returns404()
    {
        var ownTenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant
        {
            Id = ownTenantId,
            Name = "Own",
            Slug = "own",
            IsActive = true,
            Status = TenantStatuses.Active,
            CreatedAt = DateTime.UtcNow,
        });
        db.Tenants.Add(new Tenant
        {
            Id = otherTenantId,
            Name = "Other",
            Slug = "other",
            IsActive = true,
            Status = TenantStatuses.Active,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var licenseService = new Mock<IAdminTenantLicenseService>();
        var controller = CreateController(
            db,
            licenseService.Object,
            Roles.Manager,
            TenantTestDoubles.SettingsResolverReturning(ownTenantId));

        var result = await controller.Put(
            otherTenantId,
            new ExtendTenantLicenseRequest
            {
                LicenseKey = "REGK-20270101-cafe-A7F3K2D9",
                ValidUntilUtc = DateTime.UtcNow.AddDays(30),
            },
            CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
        licenseService.Verify(
            x => x.ExtendAsync(It.IsAny<Guid>(), It.IsAny<ExtendTenantLicenseRequest>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Put_Manager_RequiresLicenseKey_RejectsManualValidUntil()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe",
            Slug = "dev",
            IsActive = true,
            Status = TenantStatuses.Active,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var controller = CreateController(
            db,
            Mock.Of<IAdminTenantLicenseService>(),
            Roles.Manager,
            TenantTestDoubles.SettingsResolverReturning(tenantId));

        var missingKey = await controller.Put(
            tenantId,
            new ExtendTenantLicenseRequest { ValidUntilUtc = DateTime.UtcNow.AddDays(30) },
            CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(missingKey.Result);

        var manualDate = await controller.Put(
            tenantId,
            new ExtendTenantLicenseRequest
            {
                LicenseKey = "REGK-20270101-cafe-A7F3K2D9",
                ValidUntilUtc = DateTime.UtcNow.AddDays(30),
            },
            CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(manualDate.Result);
    }

    [Fact]
    public async Task Put_Manager_OwnTenant_DelegatesToService()
    {
        var tenantId = Guid.NewGuid();
        var overview = new TenantLicenseOverviewDto(
            new TenantLicenseStatusDto("active", "REGK-TEST", DateTime.UtcNow.AddDays(30), 30, "basic", []),
            []);

        await using var db = CreateDb();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe",
            Slug = "dev",
            IsActive = true,
            Status = TenantStatuses.Active,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var licenseService = new Mock<IAdminTenantLicenseService>();
        licenseService
            .Setup(x => x.ExtendAsync(
                tenantId,
                It.IsAny<ExtendTenantLicenseRequest>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((overview, (string?)null));

        var controller = CreateController(
            db,
            licenseService.Object,
            Roles.Manager,
            TenantTestDoubles.SettingsResolverReturning(tenantId));

        var result = await controller.Put(
            tenantId,
            new ExtendTenantLicenseRequest
            {
                LicenseKey = "REGK-20270101-cafe-A7F3K2D9",
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(overview, ok.Value);
    }

    [Fact]
    public async Task Put_Manager_InvalidLicenseKey_Returns400()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe",
            Slug = "dev",
            IsActive = true,
            Status = TenantStatuses.Active,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var controller = CreateController(
            db,
            Mock.Of<IAdminTenantLicenseService>(),
            Roles.Manager,
            TenantTestDoubles.SettingsResolverReturning(tenantId));

        var result = await controller.Put(
            tenantId,
            new ExtendTenantLicenseRequest
            {
                LicenseKey = "bad-key",
                ValidUntilUtc = DateTime.UtcNow.AddDays(30),
            },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Put_Manager_RateLimited_Returns429()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe",
            Slug = "dev",
            IsActive = true,
            Status = TenantStatuses.Active,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var rateLimiter = new Mock<ITenantLicenseExtensionRateLimiter>();
        rateLimiter
            .Setup(x => x.TryAcquireOrError(It.IsAny<string?>(), tenantId))
            .Returns("Too many license extension attempts. Please try again later.");

        var controller = CreateController(
            db,
            Mock.Of<IAdminTenantLicenseService>(),
            Roles.Manager,
            TenantTestDoubles.SettingsResolverReturning(tenantId),
            rateLimiter.Object);

        var result = await controller.Put(
            tenantId,
            new ExtendTenantLicenseRequest
            {
                LicenseKey = "REGK-20270101-cafe-A7F3K2D9",
            },
            CancellationToken.None);

        Assert.IsType<ObjectResult>(result.Result);
        var objectResult = (ObjectResult)result.Result!;
        Assert.Equal(StatusCodes.Status429TooManyRequests, objectResult.StatusCode);
    }

    [Fact]
    public async Task ConvertToPaid_SuperAdmin_ReturnsOk()
    {
        var tenantId = Guid.NewGuid();
        var saleId = Guid.NewGuid();
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Trial",
            Slug = "trial-sa",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var conversion = new Mock<ITrialConversionService>();
        conversion
            .Setup(c => c.ConvertToPaidAsync(
                tenantId,
                saleId,
                true,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                new TrialConversionResult(
                    true,
                    tenantId,
                    saleId,
                    DateTime.UtcNow.AddDays(365),
                    DateTime.UtcNow,
                    5,
                    "12_months",
                    "REGK-OK",
                    Message: "ok"),
                (string?)null));

        var controller = CreateController(
            db,
            Mock.Of<IAdminTenantLicenseService>(),
            Roles.SuperAdmin,
            Mock.Of<ISettingsTenantResolver>(),
            conversionService: conversion.Object);

        var result = await controller.ConvertToPaid(
            tenantId,
            new ConvertToPaidRequest { LicenseSaleId = saleId, AddRemainingTrialDays = true },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<TrialConversionResult>(ok.Value);
        Assert.True(body.Success);
    }

    [Fact]
    public async Task ConvertToPaid_Manager_OwnTenant_ReturnsOk()
    {
        var tenantId = Guid.NewGuid();
        var saleId = Guid.NewGuid();
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Own",
            Slug = "own-trial",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var resolver = new Mock<ISettingsTenantResolver>();
        resolver.Setup(r => r.ResolveEffectiveTenantIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenantId);

        var conversion = new Mock<ITrialConversionService>();
        conversion
            .Setup(c => c.ConvertToPaidAsync(
                tenantId,
                saleId,
                true,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                new TrialConversionResult(
                    true,
                    tenantId,
                    saleId,
                    DateTime.UtcNow.AddDays(100),
                    DateTime.UtcNow,
                    0,
                    "12_months",
                    "REGK-MGR"),
                (string?)null));

        var controller = CreateController(
            db,
            Mock.Of<IAdminTenantLicenseService>(),
            Roles.Manager,
            resolver.Object,
            conversionService: conversion.Object);

        var result = await controller.ConvertToPaid(
            tenantId,
            new ConvertToPaidRequest { LicenseSaleId = saleId },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task ConvertToPaid_Manager_CrossTenant_Returns404()
    {
        var ownTenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant
        {
            Id = otherTenantId,
            Name = "Other",
            Slug = "other-trial",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var resolver = new Mock<ISettingsTenantResolver>();
        resolver.Setup(r => r.ResolveEffectiveTenantIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ownTenantId);

        var controller = CreateController(
            db,
            Mock.Of<IAdminTenantLicenseService>(),
            Roles.Manager,
            resolver.Object);

        var result = await controller.ConvertToPaid(
            otherTenantId,
            new ConvertToPaidRequest { LicenseSaleId = Guid.NewGuid() },
            CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task ConvertToPaid_NotInTrial_Returns400()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Paid",
            Slug = "paid-now",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var conversion = new Mock<ITrialConversionService>();
        conversion
            .Setup(c => c.ConvertToPaidAsync(
                tenantId,
                It.IsAny<Guid>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(((TrialConversionResult?)null, "Tenant is not in an open trial (active or expired grace)."));

        var controller = CreateController(
            db,
            Mock.Of<IAdminTenantLicenseService>(),
            Roles.SuperAdmin,
            Mock.Of<ISettingsTenantResolver>(),
            conversionService: conversion.Object);

        var result = await controller.ConvertToPaid(
            tenantId,
            new ConvertToPaidRequest { LicenseSaleId = Guid.NewGuid() },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    private static AdminTenantLicensesController CreateController(
        AppDbContext db,
        IAdminTenantLicenseService licenseService,
        string role,
        ISettingsTenantResolver tenantResolver,
        ITenantLicenseExtensionRateLimiter? rateLimiter = null,
        ITrialConversionService? conversionService = null)
    {
        var unified = new UnifiedLicenseService(
            db,
            Mock.Of<ILicenseService>(),
            Mock.Of<KasseAPI_Final.Services.Billing.ITenantLicenseService>(),
            Mock.Of<ILicenseStatusCache>(),
            Mock.Of<ICurrentTenantAccessor>(),
            NullLogger<UnifiedLicenseService>.Instance);

        var controller = new AdminTenantLicensesController(
            licenseService,
            Mock.Of<IAdminTenantLicenseKeyService>(),
            unified,
            Mock.Of<ILicenseRenewalService>(),
            Mock.Of<IAuthorizationService>(),
            tenantResolver,
            rateLimiter ?? Mock.Of<ITenantLicenseExtensionRateLimiter>(),
            conversionService ?? Mock.Of<ITrialConversionService>(),
            db,
            NullLogger<AdminTenantLicensesController>.Instance);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, role),
            new(PermissionCatalog.PermissionClaimType, AppPermissions.LicenseManage),
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    claims,
                    authenticationType: "Test",
                    nameType: ClaimTypes.Name,
                    roleType: ClaimTypes.Role)),
            },
        };
        return controller;
    }
}
