using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantLicenseOverviewStatusMapperTests
{
    private static readonly DateTime Now = new(2026, 6, 22, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(null, null, "no_license")]
    [InlineData("2026-07-22T00:00:00Z", null, "trial")]
    [InlineData("2026-06-20T00:00:00Z", "REGK-AAAA-BBBB-CCCC", "expired")]
    [InlineData("2026-06-28T00:00:00Z", "REGK-AAAA-BBBB-CCCC", "expiring_soon")]
    [InlineData("2026-12-31T00:00:00Z", "REGK-AAAA-BBBB-CCCC", "active")]
    public void ResolveStatus_MatchesOverviewKinds(
        string? validUntilUtc,
        string? licenseKey,
        string expected)
    {
        DateTime? until = validUntilUtc == null ? null : DateTime.Parse(validUntilUtc).ToUniversalTime();
        var status = TenantLicenseOverviewStatusMapper.ResolveStatus(until, licenseKey, Now);
        Assert.Equal(expected, status);
    }
}

public sealed class AdminTenantLicenseOverviewServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"LicOverview_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static AdminTenantLicenseService CreateService(AppDbContext db) =>
        new(
            db,
            Mock.Of<ILicenseSyncService>(),
            Mock.Of<ILicenseIssuanceService>(),
            Mock.Of<ILicenseReminderEmailSender>(),
            Mock.Of<IAuditLogService>(),
            NullLogger<AdminTenantLicenseService>.Instance,
            Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Production),
            Options.Create(new TseOptions { TseMode = "Device" }),
            Options.Create(new LicenseOptions { Enabled = true }),
            Mock.Of<IDevelopmentModeService>(d => d.ShouldBypassLicense() == false));

    [Fact]
    public async Task ListOverviewAsync_ReturnsNonDeletedTenantsWithOwnerFlag()
    {
        await using var db = CreateDb();
        var now = DateTime.UtcNow;
        var tenantAId = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();

        db.Tenants.AddRange(
            new Tenant
            {
                Id = tenantAId,
                Name = "Cafe Example",
                Slug = "cafe",
                Status = TenantStatuses.Active,
                IsActive = true,
                LicenseKey = "REGK-AAAA-BBBB-CCCC",
                LicenseValidUntilUtc = now.AddDays(60),
                CreatedAt = now.AddDays(-30),
            },
            new Tenant
            {
                Id = tenantBId,
                Name = "No Owner Bar",
                Slug = "bar",
                Status = TenantStatuses.Active,
                IsActive = true,
                LicenseValidUntilUtc = now.AddDays(10),
                CreatedAt = now.AddDays(-10),
            },
            new Tenant
            {
                Id = Guid.NewGuid(),
                Name = "Archived",
                Slug = "archived",
                Status = TenantStatuses.Deleted,
                IsActive = false,
                CreatedAt = now,
            });

        db.Users.Add(new ApplicationUser
        {
            Id = "owner-1",
            UserName = "owner@cafe.test",
            Email = "owner@cafe.test",
            IsActive = true,
            Role = Roles.Manager,
        });

        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            Id = Guid.NewGuid(),
            UserId = "owner-1",
            TenantId = tenantAId,
            IsActive = true,
            IsOwner = true,
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync();

        var rows = await CreateService(db).ListOverviewAsync(CancellationToken.None);

        Assert.Equal(2, rows.Count);
        var cafe = Assert.Single(rows, r => r.TenantSlug == "cafe");
        Assert.Equal("Cafe Example", cafe.TenantName);
        Assert.Equal("REGK-AAAA-BBBB-CCCC", cafe.LicenseKey);
        Assert.True(cafe.HasOwnerAdmin);
        Assert.Equal("active", cafe.Status);

        var bar = Assert.Single(rows, r => r.TenantSlug == "bar");
        Assert.False(bar.HasOwnerAdmin);
        Assert.Equal("trial", bar.Status);
    }
}

public sealed class AdminTenantsLicenseOverviewControllerTests
{
    [Fact]
    public async Task ListLicenseOverview_SuperAdmin_ReturnsServiceRows()
    {
        var expected = new List<TenantLicenseOverviewListItemDto>
        {
            new(
                Guid.NewGuid(),
                "Cafe Example",
                "cafe",
                "REGK-AAAA-BBBB-CCCC",
                DateTime.UtcNow.AddMonths(6),
                "active",
                true,
                DateTime.UtcNow),
        };

        var licenseService = new Mock<IAdminTenantLicenseService>();
        licenseService
            .Setup(x => x.ListOverviewAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = new AdminTenantsController(
            Mock.Of<IAdminTenantService>(),
            licenseService.Object,
            Mock.Of<ITenantDeletionService>(),
            Mock.Of<IAuditLogService>(),
            Mock.Of<IHostEnvironment>(),
            Mock.Of<ILogger<AdminTenantsController>>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity(
                        new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier, "super-admin"),
                            new Claim(ClaimTypes.Role, Roles.SuperAdmin),
                        },
                        authenticationType: "TestAuth")),
            },
        };

        var result = await controller.ListLicenseOverview(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var rows = Assert.IsAssignableFrom<IReadOnlyList<TenantLicenseOverviewListItemDto>>(ok.Value);
        Assert.Single(rows);
        Assert.Equal("cafe", rows[0].TenantSlug);
    }
}
