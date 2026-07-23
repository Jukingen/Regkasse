using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using IAdminTenantLicenseKeyService = KasseAPI_Final.Services.AdminTenants.ITenantLicenseService;
using IBillingTenantLicenseService = KasseAPI_Final.Services.Billing.ITenantLicenseService;

namespace KasseAPI_Final.Tests;

public sealed class AdminLicenseExtendTests
{
    [Fact]
    public async Task ExtendLicense_WithoutTenantContext_ReturnsBadRequest()
    {
        var db = CreateDb();
        var controller = CreateController(
            db,
            billingService: Mock.Of<IBillingTenantLicenseService>(),
            tenantAccessor: TenantTestDoubles.TenantAccessorReturning(null),
            tenantResolver: TenantTestDoubles.SettingsResolverReturning(Guid.Empty));

        var result = await controller.ExtendLicense(
            new ExtendLicenseRequest { LicenseKey = "REGK-20270101-cafe-A7F3K2D9" },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Tenant context required", badRequest.Value?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtendLicense_ValidRequest_ReturnsSuccessPayload()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var validUntil = DateTime.UtcNow.AddDays(365);
        const string licenseKey = "REGK-20270101-cafe-A7F3K2D9";

        var billingService = new Mock<IBillingTenantLicenseService>();
        billingService
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
                ValidUntilUtc = validUntil,
                LicensePlan = LicenseSalePlans.TwelveMonths,
            });

        var db = CreateDb(tenantId);
        var controller = CreateController(
            db,
            billingService: billingService.Object,
            tenantAccessor: TenantTestDoubles.TenantAccessorReturning(tenantId),
            tenantResolver: TenantTestDoubles.SettingsResolverReturning(tenantId),
            userId: userId);

        var result = await controller.ExtendLicense(
            new ExtendLicenseRequest { LicenseKey = licenseKey },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("Lizenz wurde erfolgreich verlängert", ok.Value?.ToString(), StringComparison.Ordinal);
        Assert.Contains(licenseKey, ok.Value?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtendLicense_ServiceFailure_ReturnsBadRequest()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var billingService = new Mock<IBillingTenantLicenseService>();
        billingService
            .Setup(x => x.ExtendLicenseAsync(
                tenantId,
                It.IsAny<string>(),
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtendResult
            {
                Success = false,
                Message = "Diese Lizenz ist bereits abgelaufen.",
            });

        var db = CreateDb(tenantId);
        var controller = CreateController(
            db,
            billingService: billingService.Object,
            tenantAccessor: TenantTestDoubles.TenantAccessorReturning(tenantId),
            tenantResolver: TenantTestDoubles.SettingsResolverReturning(tenantId),
            userId: userId);

        var result = await controller.ExtendLicense(
            new ExtendLicenseRequest { LicenseKey = "REGK-20270101-cafe-A7F3K2D9" },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("abgelaufen", badRequest.Value?.ToString(), StringComparison.Ordinal);
    }

    private static AppDbContext CreateDb(Guid? tenantId = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AdminLicenseExtend_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AppDbContext(options, NullCurrentTenantAccessor.Instance);
        if (tenantId.HasValue)
        {
            db.Tenants.Add(new Tenant
            {
                Id = tenantId.Value,
                Name = "Cafe",
                Slug = "dev",
                Status = TenantStatuses.Active,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });
            db.SaveChanges();
        }

        return db;
    }

    private static AdminLicenseController CreateController(
        AppDbContext db,
        IBillingTenantLicenseService billingService,
        ICurrentTenantAccessor tenantAccessor,
        ISettingsTenantResolver tenantResolver,
        Guid? userId = null)
    {
        var actorId = userId ?? Guid.NewGuid();
        var controller = new AdminLicenseController(
            Mock.Of<ILicenseService>(),
            Mock.Of<ILicenseIssuanceService>(),
            Mock.Of<ILicenseRenewalService>(),
            Mock.Of<IAdminTenantLicenseService>(),
            Mock.Of<IAdminTenantLicenseKeyService>(),
            billingService,
            tenantAccessor,
            db,
            Mock.Of<IAdminTenantService>(),
            tenantResolver,
            Mock.Of<ILicenseReminderNotificationStore>(),
            Mock.Of<IAuditLogService>(),
            Mock.Of<ILicenseExportService>(),
            NullLogger<AdminLicenseController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, actorId.ToString("D")),
                    new Claim(ClaimTypes.Role, Roles.Manager),
                ],
                authenticationType: "Test")),
            },
        };

        return controller;
    }
}
