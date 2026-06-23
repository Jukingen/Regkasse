using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Tenancy;
using IAdminTenantLicenseKeyService = KasseAPI_Final.Services.AdminTenants.ITenantLicenseService;
using IBillingTenantLicenseService = KasseAPI_Final.Services.Billing.ITenantLicenseService;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminLicenseDashboardTests
{
    private static readonly Guid TenantAId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantBId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"LicDash_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static AdminLicenseController CreateController(AppDbContext db, string actorId, string actorRole)
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
            NullLogger<AdminLicenseController>.Instance);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, actorId),
            new(ClaimTypes.Role, actorRole),
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

    private static AdminLicenseController CreateController(
        AppDbContext db,
        string actorId,
        string actorRole,
        IAdminTenantService adminTenantService)
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
            adminTenantService,
            TenantTestDoubles.PrimaryTenantResolver,
            Mock.Of<ILicenseReminderNotificationStore>(),
            Mock.Of<IAuditLogService>(),
            NullLogger<AdminLicenseController>.Instance);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, actorId),
            new(ClaimTypes.Role, actorRole),
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
    public async Task GetDashboardStats_SuperAdmin_ReturnsAllTenantLicenses()
    {
        // Super Admin tüm tenant'ların lisans durumunu görebilmeli
        await using var db = CreateDb();
        var now = DateTime.UtcNow;

        db.Tenants.AddRange(
            new Tenant
            {
                Id = TenantAId,
                Name = "Active Co",
                Slug = "active-co",
                Status = TenantStatuses.Active,
                IsActive = true,
                LicenseValidUntilUtc = now.AddDays(60),
                CreatedAt = now,
            },
            new Tenant
            {
                Id = TenantBId,
                Name = "Expiring Co",
                Slug = "expiring-co",
                Status = TenantStatuses.Active,
                IsActive = true,
                LicenseValidUntilUtc = now.AddDays(10),
                CreatedAt = now,
            },
            new Tenant
            {
                Id = Guid.NewGuid(),
                Name = "Expired Co",
                Slug = "expired-co",
                Status = TenantStatuses.Active,
                IsActive = true,
                LicenseValidUntilUtc = now.AddDays(-1),
                CreatedAt = now,
            },
            new Tenant
            {
                Id = Guid.NewGuid(),
                Name = "Deleted Co",
                Slug = "deleted-co",
                Status = TenantStatuses.Deleted,
                IsActive = false,
                LicenseValidUntilUtc = now.AddDays(60),
                CreatedAt = now,
            });

        await db.SaveChangesAsync();

        var controller = CreateController(db, "super-1", Roles.SuperAdmin);
        var result = await controller.GetDashboardStats(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var stats = Assert.IsType<LicenseDashboardStatsDto>(ok.Value);

        Assert.Equal(2, stats.ActiveTenantLicenses);
        Assert.Equal(1, stats.ExpiringTenantLicenses);
        Assert.Equal(1, stats.ExpiredTenantLicenses);
    }

    [Fact]
    public async Task GetDashboardStats_Manager_ReturnsOnlyOwnTenantLicense()
    {
        // Manager sadece kendi tenant'ının lisans durumunu görebilmeli
        await using var db = CreateDb();
        var now = DateTime.UtcNow;

        db.Tenants.AddRange(
            new Tenant
            {
                Id = TenantAId,
                Name = "Mine",
                Slug = "mine",
                Status = TenantStatuses.Active,
                IsActive = true,
                LicenseValidUntilUtc = now.AddDays(10),
                CreatedAt = now,
            },
            new Tenant
            {
                Id = TenantBId,
                Name = "Other",
                Slug = "other",
                Status = TenantStatuses.Active,
                IsActive = true,
                LicenseValidUntilUtc = now.AddDays(60),
                CreatedAt = now,
            },
            new Tenant
            {
                Id = Guid.NewGuid(),
                Name = "Foreign expired",
                Slug = "foreign-expired",
                Status = TenantStatuses.Active,
                IsActive = true,
                LicenseValidUntilUtc = now.AddDays(-5),
                CreatedAt = now,
            });

        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            Id = Guid.NewGuid(),
            UserId = "manager-1",
            TenantId = TenantAId,
            IsActive = true,
            IsOwner = true,
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync();

        var controller = CreateController(db, "manager-1", Roles.Manager);
        var result = await controller.GetDashboardStats(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var stats = Assert.IsType<LicenseDashboardStatsDto>(ok.Value);

        Assert.Equal(1, stats.ActiveTenantLicenses);
        Assert.Equal(1, stats.ExpiringTenantLicenses);
        Assert.Equal(0, stats.ExpiredTenantLicenses);
    }

    [Fact]
    public async Task GetDashboardStats_IncludesDeploymentAndDeviceMetrics()
    {
        await using var db = CreateDb();
        var now = DateTime.UtcNow;

        db.IssuedLicenses.AddRange(
            new IssuedLicense
            {
                Id = Guid.NewGuid(),
                LicenseKey = "REGK-AAAAA-BBBBB-CCCCC",
                CustomerName = "Active deployment",
                ExpiryAtUtc = now.AddDays(90),
                RequireFingerprint = false,
                SignedJwt = "jwt-active",
                IssuedAtUtc = now,
            },
            new IssuedLicense
            {
                Id = Guid.NewGuid(),
                LicenseKey = "REGK-DDDDD-EEEEE-FFFFF",
                CustomerName = "Expiring deployment",
                ExpiryAtUtc = now.AddDays(15),
                RequireFingerprint = false,
                SignedJwt = "jwt-expiring",
                IssuedAtUtc = now,
            },
            new IssuedLicense
            {
                Id = Guid.NewGuid(),
                LicenseKey = "REGK-GGGGG-HHHHH-IIIII",
                CustomerName = "Expired deployment",
                ExpiryAtUtc = now.AddDays(-2),
                RequireFingerprint = false,
                SignedJwt = "jwt-expired",
                IssuedAtUtc = now.AddDays(-30),
            });

        db.ActivatedLicenses.AddRange(
            new ActivatedLicense
            {
                Id = Guid.NewGuid(),
                LicenseKey = "REGK-AAAAA-BBBBB-CCCCC",
                ValidUntilUtc = now.AddDays(90),
                MachineFingerprint = "machine-a",
                ActivatedAtUtc = now,
                LastSeenAtUtc = now,
                IsActive = true,
            },
            new ActivatedLicense
            {
                Id = Guid.NewGuid(),
                LicenseKey = "REGK-AAAAA-BBBBB-CCCCC",
                ValidUntilUtc = now.AddDays(90),
                MachineFingerprint = "machine-a",
                ActivatedAtUtc = now,
                LastSeenAtUtc = now,
                IsActive = true,
            },
            new ActivatedLicense
            {
                Id = Guid.NewGuid(),
                LicenseKey = "REGK-DDDDD-EEEEE-FFFFF",
                ValidUntilUtc = now.AddDays(15),
                MachineFingerprint = "machine-b",
                ActivatedAtUtc = now,
                LastSeenAtUtc = now,
                IsActive = true,
            });

        await db.SaveChangesAsync();

        var controller = CreateController(db, "super-1", Roles.SuperAdmin);
        var result = await controller.GetDashboardStats(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var stats = Assert.IsType<LicenseDashboardStatsDto>(ok.Value);

        Assert.Equal(2, stats.ActiveDeploymentLicenses);
        Assert.Equal(1, stats.ExpiringDeploymentLicenses);
        Assert.Equal(1, stats.ExpiredDeploymentLicenses);
        Assert.Equal(2, stats.ActivatedDevices);
        Assert.NotEmpty(stats.RecentActivities);
        Assert.Contains(stats.RecentActivities, a => a.Action == "GENERATED");
        Assert.Contains(stats.RecentActivities, a => a.Action == "ACTIVATED");
    }

    [Fact]
    public async Task GetDashboardSummary_ReturnsDeploymentLicenseCountsOnly()
    {
        await using var db = CreateDb();
        var now = DateTime.UtcNow;

        db.Tenants.Add(new Tenant
        {
            Id = TenantAId,
            Name = "Tenant",
            Slug = "tenant",
            Status = TenantStatuses.Active,
            IsActive = true,
            LicenseValidUntilUtc = now.AddDays(60),
            CreatedAt = now,
        });

        db.IssuedLicenses.Add(new IssuedLicense
        {
            Id = Guid.NewGuid(),
            LicenseKey = "REGK-AAAAA-BBBBB-CCCCC",
            CustomerName = "Deployment",
            ExpiryAtUtc = now.AddDays(90),
            RequireFingerprint = false,
            SignedJwt = "jwt",
            IssuedAtUtc = now,
        });

        await db.SaveChangesAsync();

        var controller = CreateController(db, "super-1", Roles.SuperAdmin);
        var result = await controller.GetDashboardSummary(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var summary = Assert.IsType<LicenseDashboardSummaryResponse>(ok.Value);

        Assert.Equal(1, summary.ActiveLicenses);
        Assert.Equal(0, summary.ExpiringWithin30Days);
        Assert.Equal(0, summary.ExpiredLicenses);
    }

    [Fact]
    public async Task GetRecentLicenseActivities_ReadsAuditLogWithMetadataAndUserEmail()
    {
        await using var db = CreateDb();
        var now = DateTime.UtcNow;
        var issuedId = Guid.NewGuid();

        db.Users.Add(new ApplicationUser
        {
            Id = "admin-1",
            UserName = "admin@test.local",
            Email = "admin@test.local",
            FirstName = "Admin",
            LastName = "User",
            IsActive = true,
            Role = Roles.SuperAdmin,
        });

        db.IssuedLicenses.Add(new IssuedLicense
        {
            Id = issuedId,
            LicenseKey = "REGK-AAAAA-BBBBB-CCCCC",
            CustomerName = "Revoked Co",
            ExpiryAtUtc = now.AddDays(30),
            RequireFingerprint = true,
            MachineHashHex = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
            SignedJwt = "jwt",
            IssuedAtUtc = now.AddDays(-1),
            IsRevoked = true,
        });

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = LegacyDefaultTenantIds.Primary,
            SessionId = Guid.NewGuid().ToString(),
            UserId = "admin-1",
            UserRole = Roles.SuperAdmin,
            Action = "LIC_REVOKE",
            EntityType = nameof(IssuedLicense),
            EntityId = issuedId,
            Status = AuditLogStatus.Success,
            Timestamp = now,
            Metadata = """{"MachineHash":"abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"}""",
            NewValues = """{"licenseKeyMasked":"REGK-****-****-CCCCC"}""",
        });

        await db.SaveChangesAsync();

        var controller = CreateController(db, "super-1", Roles.SuperAdmin);
        var result = await controller.GetDashboardStats(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var stats = Assert.IsType<LicenseDashboardStatsDto>(ok.Value);
        var revoked = Assert.Single(stats.RecentActivities, a => a.Action == "REVOKED");

        Assert.Equal("REGK-****-****-CCCCC", revoked.LicenseKey);
        Assert.Equal("admin@test.local", revoked.UserEmail);
        Assert.False(string.IsNullOrEmpty(revoked.MachineHash));
    }

    [Fact]
    public async Task GetAllTenantLicenses_Filters_By_Search_Status_And_LicenseStatus()
    {
        await using var db = CreateDb();
        var now = DateTime.UtcNow;

        var service = new Mock<IAdminTenantService>();
        service.Setup(x => x.ListAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new AdminTenantListItemDto(
                    Guid.NewGuid(),
                    "Cafe Alpha",
                    "cafe-alpha",
                    null,
                    null,
                    TenantStatuses.Active,
                    true,
                    "REGK-AAAAA-BBBBB-CCCCC",
                    now.AddDays(14),
                    now.AddDays(-20),
                    null,
                    14,
                    "alpha@tenant.test",
                    false),
                new AdminTenantListItemDto(
                    Guid.NewGuid(),
                    "Beta Store",
                    "beta-store",
                    null,
                    null,
                    TenantStatuses.Suspended,
                    false,
                    null,
                    null,
                    now.AddDays(-30),
                    null,
                    null,
                    "beta@tenant.test",
                    false),
            ]);

        var controller = CreateController(db, "super-1", Roles.SuperAdmin, service.Object);

        var result = await controller.GetAllTenantLicenses(
            search: "alpha",
            status: TenantStatuses.Active,
            licenseStatus: "active",
            cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var rows = Assert.IsType<List<TenantLicenseDto>>(ok.Value);
        var row = Assert.Single(rows);

        Assert.Equal("Cafe Alpha", row.Name);
        Assert.Equal("alpha@tenant.test", row.OwnerEmail);
        Assert.Equal("REGK-****-****-CCCCC", row.LicenseKey);
        Assert.Equal("active", row.LicenseStatus);
    }
}
