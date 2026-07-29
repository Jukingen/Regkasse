using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
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
using IAdminTenantLicenseKeyService = KasseAPI_Final.Services.AdminTenants.ITenantLicenseService;
using IBillingTenantLicenseService = KasseAPI_Final.Services.Billing.ITenantLicenseService;

namespace KasseAPI_Final.Tests;

public sealed class AdminLicenseGracePeriodDashboardTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"LicGraceDash_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static AdminLicenseController CreateController(AppDbContext db)
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
            NullLogger<AdminLicenseController>.Instance);

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
            },
        };
        return controller;
    }

    [Fact]
    public async Task GetGracePeriodDashboard_BucketsTenantsByRemainingDays()
    {
        await using var db = CreateDb();
        var now = DateTime.UtcNow;

        // Critical (≤2): 5 days overdue → 2 remaining
        SeedTenant(db, "crit", now.AddDays(-5));
        // Medium (3–5): 3 days overdue → 4 remaining
        SeedTenant(db, "mid", now.AddDays(-3));
        // Good (≥6): 1 day overdue → 6 remaining
        SeedTenant(db, "good", now.AddDays(-1));
        // Locked (outside grace): ignored
        SeedTenant(db, "locked", now.AddDays(-10));
        // Active: ignored
        SeedTenant(db, "active", now.AddDays(20));
        await db.SaveChangesAsync();

        var sut = CreateController(db);
        var result = await sut.GetGracePeriodDashboard(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<GracePeriodDashboardDto>(ok.Value);

        Assert.Equal(3, dto.Total);
        Assert.Equal(1, dto.Critical);
        Assert.Equal(1, dto.Medium);
        Assert.Equal(1, dto.Good);
        Assert.Equal(3, dto.List.Count);
        Assert.Equal("crit", dto.List[0].Slug);
        Assert.True(dto.List[0].DaysRemaining <= 2);
        Assert.All(dto.List, row => Assert.True(row.LockdownAtUtc > row.ExpiredAtUtc));
    }

    private static void SeedTenant(AppDbContext db, string slug, DateTime validUntilUtc)
    {
        db.Tenants.Add(new Tenant
        {
            Id = Guid.NewGuid(),
            Name = $"Tenant {slug}",
            Slug = slug,
            Status = TenantStatuses.Active,
            IsActive = true,
            LicenseValidUntilUtc = validUntilUtc,
            LicenseKey = $"REGK-20270101-{slug}-TESTKEY1",
            CreatedAt = DateTime.UtcNow,
        });
    }
}
