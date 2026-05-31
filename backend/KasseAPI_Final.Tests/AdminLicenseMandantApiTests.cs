using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminLicenseMandantApiTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"LicMandant_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    [Fact]
    public async Task Renew_WithTenantId_RequiresPaymentConfirmed()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb();
        var controller = CreateController(db, Mock.Of<ILicenseRenewalService>(), Roles.SuperAdmin);

        var action = await controller.Renew(
            new RenewLicenseRequestBody
            {
                TenantId = tenantId,
                AdditionalMonths = 12,
                PaymentConfirmed = false,
            },
            CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.NotNull(bad.Value);
    }

    [Fact]
    public async Task Renew_WithTenantId_DelegatesToRenewalService()
    {
        var tenantId = Guid.NewGuid();
        var renewalMock = new Mock<ILicenseRenewalService>();
        renewalMock
            .Setup(x => x.RenewLicenseAsync(
                tenantId,
                12,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LicenseRenewalResult
            {
                Success = true,
                NewExpiryDate = DateTime.UtcNow.AddYears(1),
                DaysAdded = 360,
                Message = "ok",
            });

        await using var db = CreateDb();
        var controller = CreateController(db, renewalMock.Object, Roles.SuperAdmin);

        var result = await controller.Renew(
            new RenewLicenseRequestBody
            {
                TenantId = tenantId,
                AdditionalMonths = 12,
                PaymentConfirmed = true,
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<LicenseRenewalResult>(ok.Value);
        Assert.True(payload.Success);
    }

    [Fact]
    public async Task GetMandantHistory_WhenTenantMissing_Returns404()
    {
        await using var db = CreateDb();
        var licenseServiceMock = new Mock<IAdminTenantLicenseService>();
        licenseServiceMock
            .Setup(x => x.GetOverviewAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantLicenseOverviewDto?)null);

        var controller = CreateController(db, Mock.Of<ILicenseRenewalService>(), licenseServiceMock.Object);

        var result = await controller.GetMandantHistory(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    private static AdminLicenseController CreateController(
        AppDbContext db,
        ILicenseRenewalService renewalService,
        string role = Roles.Manager)
    {
        return CreateController(db, renewalService, Mock.Of<IAdminTenantLicenseService>(), role);
    }

    private static AdminLicenseController CreateController(
        AppDbContext db,
        ILicenseRenewalService renewalService,
        IAdminTenantLicenseService tenantLicenseService,
        string role = Roles.Manager)
    {
        var controller = new AdminLicenseController(
            Mock.Of<ILicenseService>(),
            Mock.Of<ILicenseIssuanceService>(),
            renewalService,
            tenantLicenseService,
            db,
            Mock.Of<IAdminTenantService>(),
            Mock.Of<ILicenseReminderNotificationStore>(),
            Mock.Of<IAuditLogService>(),
            NullLogger<AdminLicenseController>.Instance);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, role),
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
