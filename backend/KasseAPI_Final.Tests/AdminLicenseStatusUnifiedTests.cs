using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminTenants;
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

public sealed class AdminLicenseStatusUnifiedTests
{
    [Fact]
    public async Task GetStatus_UsesUnifiedDeploymentSnapshot()
    {
        var snapshot = new LicenseStatusResponse(true, false, false, 42, DateTime.UtcNow.AddDays(42), "hash");
        var unified = new Mock<IUnifiedLicenseService>();
        unified
            .Setup(x => x.GetUnifiedStatusAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UnifiedLicenseStatusDto
            {
                IsActive = true,
                DeploymentSnapshot = snapshot,
            });

        var reminders = new Mock<ILicenseReminderNotificationStore>();
        reminders.Setup(x => x.GetReminders()).Returns([]);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AdminLicStatus_{Guid.NewGuid():N}")
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
            TenantTestDoubles.PrimaryTenantResolver,
            reminders.Object,
            Mock.Of<IAuditLogService>(),
            Mock.Of<ILicenseExportService>(),
            unified.Object,
            NullLogger<AdminLicenseController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var result = await controller.GetStatus(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<LicenseStatusResponse>(ok.Value);
        Assert.True(payload.IsValid);
        Assert.Equal(42, payload.DaysRemaining);
        unified.Verify(x => x.GetUnifiedStatusAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
