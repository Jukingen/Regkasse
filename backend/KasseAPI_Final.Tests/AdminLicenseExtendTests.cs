using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Services.License;
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
    public void ExtendLicense_RedirectsToUnifiedActivate()
    {
        var controller = CreateController();

        var result = controller.ExtendLicense(
            new ExtendLicenseRequest { LicenseKey = "REGK-20270101-cafe-A7F3K2D9" });

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.True(redirect.Permanent);
        Assert.True(redirect.PreserveMethod);
        Assert.Equal(UnifiedLicenseRoutes.Activate, redirect.Url);
    }

    [Fact]
    public void ActivateLicenseDeprecated_RedirectsToUnifiedActivate()
    {
        var controller = CreateController();

        var result = controller.ActivateLicenseDeprecated(
            new ActivateLicenseRequest { LicenseKey = "REGK-20270101-cafe-A7F3K2D9" });

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.True(redirect.Permanent);
        Assert.True(redirect.PreserveMethod);
        Assert.Equal(UnifiedLicenseRoutes.Activate, redirect.Url);
    }

    private static AdminLicenseController CreateController()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AdminLicenseExtend_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AppDbContext(options, NullCurrentTenantAccessor.Instance);

        var controller = new AdminLicenseController(
            Mock.Of<ILicenseService>(),
            Mock.Of<ILicenseIssuanceService>(),
            Mock.Of<ILicenseRenewalService>(),
            Mock.Of<IAdminTenantLicenseService>(),
            Mock.Of<IAdminTenantLicenseKeyService>(),
            Mock.Of<IBillingTenantLicenseService>(),
            TenantTestDoubles.TenantAccessorReturning(null),
            db,
            Mock.Of<IAdminTenantService>(),
            TenantTestDoubles.SettingsResolverReturning(Guid.Empty),
            Mock.Of<ILicenseReminderNotificationStore>(),
            Mock.Of<IAuditLogService>(),
            Mock.Of<ILicenseExportService>(),
            Mock.Of<IUnifiedLicenseService>(),
            NullLogger<AdminLicenseController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("D")),
                    new Claim(ClaimTypes.Role, Roles.Manager),
                ],
                authenticationType: "Test")),
            },
        };

        return controller;
    }
}
