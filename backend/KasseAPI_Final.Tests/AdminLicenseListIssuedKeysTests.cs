using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
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

public sealed class AdminLicenseListIssuedKeysTests
{
    private const string UnifiedKey = "REGK-20990101-SYSTEM-ABCDEF12";
    private const string LegacyKey = "REGK-AAAAA-BBBBB-CCCCC";

    [Fact]
    public async Task ListIssuedLicenses_SuperAdmin_ReturnsFullLicenseKey()
    {
        await using var db = CreateDb();
        SeedIssued(db, UnifiedKey);
        var controller = CreateController(db, Roles.SuperAdmin);

        var payload = await ListAsync(controller);

        Assert.Equal(UnifiedKey, payload.Items[0].LicenseKey);
    }

    [Fact]
    public async Task ListIssuedLicenses_SystemCriticalClaim_ReturnsFullLicenseKey()
    {
        await using var db = CreateDb();
        SeedIssued(db, UnifiedKey);
        var controller = CreateController(
            db,
            Roles.Manager,
            extraClaims: [new Claim(PermissionCatalog.PermissionClaimType, AppPermissions.SystemCritical)]);

        var payload = await ListAsync(controller);

        Assert.Equal(UnifiedKey, payload.Items[0].LicenseKey);
    }

    [Fact]
    public async Task ListIssuedLicenses_Manager_MasksUnifiedLicenseKey()
    {
        await using var db = CreateDb();
        SeedIssued(db, UnifiedKey);
        var controller = CreateController(db, Roles.Manager);

        var payload = await ListAsync(controller);

        Assert.Equal("REGK-****-****-ABCDEF12", payload.Items[0].LicenseKey);
    }

    [Fact]
    public async Task ListIssuedLicenses_Manager_MasksLegacyFourPartKeyLastSegmentOnly()
    {
        await using var db = CreateDb();
        SeedIssued(db, LegacyKey);
        var controller = CreateController(db, Roles.Manager);

        var payload = await ListAsync(controller);

        Assert.Equal("REGK-****-****-CCCCC", payload.Items[0].LicenseKey);
    }

    private static async Task<IssuedLicensesListResponse> ListAsync(AdminLicenseController controller)
    {
        var result = await controller.ListIssuedLicenses();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<IssuedLicensesListResponse>(ok.Value);
    }

    private static void SeedIssued(AppDbContext db, string licenseKey)
    {
        db.IssuedLicenses.Add(new IssuedLicense
        {
            Id = Guid.NewGuid(),
            LicenseKey = licenseKey,
            CustomerName = "Acme",
            ExpiryAtUtc = DateTime.UtcNow.AddYears(1),
            RequireFingerprint = false,
            SignedJwt = "jwt",
            IssuedAtUtc = DateTime.UtcNow,
        });
        db.SaveChanges();
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"LicListKeys_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, new CurrentTenantAccessor { TenantId = SystemTenantIds.Platform });
    }

    private static AdminLicenseController CreateController(
        AppDbContext db,
        string actorRole,
        IReadOnlyList<Claim>? extraClaims = null)
    {
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

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "actor-1"),
            new(ClaimTypes.Role, actorRole),
        };
        if (extraClaims is not null)
            claims.AddRange(extraClaims);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
            },
        };
        return controller;
    }
}
