using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Services.License;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using IAdminTenantLicenseKeyService = KasseAPI_Final.Services.AdminTenants.ITenantLicenseService;
using IBillingTenantLicenseService = KasseAPI_Final.Services.Billing.ITenantLicenseService;

namespace KasseAPI_Final.Tests;

public sealed class AdminLicenseReminderEmailPreviewTests
{
    [Fact]
    public void GetReminderEmailPreview_ReturnsSampleHtmlForSuperAdmin()
    {
        var controller = CreateController();
        var result = controller.GetReminderEmailPreview(
            daysUntilExpiry: 7,
            tenantName: null,
            adminName: null,
            expiryDate: null,
            Options.Create(new LicenseOptions { AdminLicenseUrl = "https://admin.regkasse.at/license" }),
            Options.Create(new EmailSmtpOptions { SupportContact = "support@regkasse.at" }));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<LicenseReminderEmailPreviewDto>(ok.Value);
        Assert.Equal(7, dto.DaysUntilExpiry);
        Assert.Contains("[Erinnerung]", dto.Subject, StringComparison.Ordinal);
        Assert.Contains("Cafe Muster", dto.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("mailto:support@regkasse.at", dto.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Jetzt verlängern", dto.PlainBody, StringComparison.OrdinalIgnoreCase);
    }

    private static AdminLicenseController CreateController()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"LicPreview_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AppDbContext(
            options,
            new CurrentTenantAccessor { TenantId = SystemTenantIds.Platform });

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
            TenantTestDoubles.PrimaryTenantResolver,
            Mock.Of<ILicenseReminderNotificationStore>(),
            Mock.Of<IAuditLogService>(),
            Mock.Of<ILicenseExportService>(),
            Mock.Of<IUnifiedLicenseService>(),
            NullLogger<AdminLicenseController>.Instance);

        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(new LicenseOptions()));
        services.AddSingleton(Options.Create(new EmailSmtpOptions()));
        var sp = services.BuildServiceProvider();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "sa-1"),
            new(ClaimTypes.Role, Roles.SuperAdmin),
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
                RequestServices = sp,
            },
        };
        return controller;
    }
}
