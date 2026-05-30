using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PermissionServiceTests
{
    [Fact]
    public async Task HasPermissionAsync_SuperAdmin_ReturnsTrue_For_Any_Permission()
    {
        var (db, userManager) = await CreateSetupAsync();
        var userId = await SeedUserAsync(userManager, role: Roles.SuperAdmin);
        var svc = CreateService(db, userManager, effective: new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var allowed = await svc.HasPermissionAsync(userId, AppPermissions.SystemCritical);

        Assert.True(allowed);
    }

    [Fact]
    public async Task HasPermissionAsync_UsesEffectivePermissions_WithImplication()
    {
        var (db, userManager) = await CreateSetupAsync();
        var userId = await SeedUserAsync(userManager, role: Roles.Manager);
        var svc = CreateService(db, userManager, effective: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AppPermissions.UserManage,
        });

        Assert.True(await svc.HasPermissionAsync(userId, AppPermissions.UserCreate));
        Assert.False(await svc.HasPermissionAsync(userId, AppPermissions.SystemCritical));
    }

    [Fact]
    public async Task GetUserOverridesAsync_Filters_By_Tenant()
    {
        var (db, userManager) = await CreateSetupAsync();
        var userId = await SeedUserAsync(userManager, role: Roles.Manager);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        db.UserPermissionOverrides.AddRange(
            new UserPermissionOverride { UserId = userId, TenantId = tenantA, Permission = AppPermissions.ReportExport, IsGranted = true },
            new UserPermissionOverride { UserId = userId, TenantId = tenantB, Permission = AppPermissions.AuditExport, IsGranted = true });
        await db.SaveChangesAsync();

        var svc = CreateService(db, userManager);
        var forTenantA = await svc.GetUserOverridesAsync(userId, tenantA);

        Assert.Single(forTenantA);
        Assert.Equal(AppPermissions.ReportExport, forTenantA[0].Permission);
    }

    [Fact]
    public async Task AddOrUpdatePermissionOverrideAsync_Upserts_And_Logs_Audit()
    {
        var (db, userManager) = await CreateSetupAsync();
        var userId = await SeedUserAsync(userManager, role: Roles.Manager);
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogUserLifecycleAsync(
                AuditEventType.UserPermissionOverridesChanged,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<UserCreatedAuditDetails?>()))
            .ReturnsAsync(new AuditLog());

        var svc = CreateService(db, userManager, audit: audit.Object);
        await svc.AddOrUpdatePermissionOverrideAsync(
            userId,
            AppPermissions.ReportExport,
            isGranted: true,
            reason: "Test grant",
            expiresAt: null,
            actorUserId: "admin-1");

        var row = await db.UserPermissionOverrides.SingleAsync();
        Assert.True(row.IsGranted);
        Assert.Equal(AppPermissions.ReportExport, row.Permission);
        Assert.Equal("admin-1", row.CreatedByUserId);

        await svc.AddOrUpdatePermissionOverrideAsync(
            userId,
            AppPermissions.ReportExport,
            isGranted: false,
            reason: "Revoke",
            expiresAt: null,
            actorUserId: "admin-1");

        Assert.False((await db.UserPermissionOverrides.SingleAsync()).IsGranted);
        audit.Verify(a => a.LogUserLifecycleAsync(
            AuditEventType.UserPermissionOverridesChanged,
            "admin-1",
            It.IsAny<string>(),
            userId,
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            AuditLogStatus.Success,
            It.IsAny<string?>(),
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            It.IsAny<UserCreatedAuditDetails?>()), Times.AtLeastOnce);
    }

    private static PermissionService CreateService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IReadOnlySet<string>? effective = null,
        IAuditLogService? audit = null)
    {
        var effectiveMock = new Mock<IEffectivePermissionResolver>();
        effectiveMock.Setup(r => r.GetEffectivePermissionsAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(effective ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        return new PermissionService(
            db,
            userManager,
            effectiveMock.Object,
            audit ?? Mock.Of<IAuditLogService>(),
            Mock.Of<IHttpContextAccessor>(),
            NullCurrentTenantAccessor.Instance);
    }

    private static async Task<(AppDbContext Db, UserManager<ApplicationUser> UserManager)> CreateSetupAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PermSvc_{Guid.NewGuid():N}")
            .Options;
        var db = new AppDbContext(options, NullCurrentTenantAccessor.Instance);
        var store = new UserStore<ApplicationUser, IdentityRole, AppDbContext>(db);
        var userManager = new UserManager<ApplicationUser>(store, null!, null!, null!, null!, null!, null!, null!, null!);
        return (db, userManager);
    }

    private static async Task<string> SeedUserAsync(UserManager<ApplicationUser> userManager, string role)
    {
        var user = new ApplicationUser
        {
            UserName = $"user_{Guid.NewGuid():N}@test.local",
            Email = $"user_{Guid.NewGuid():N}@test.local",
            Role = role,
            IsActive = true,
            EmailConfirmed = true,
        };
        var result = await userManager.CreateAsync(user);
        Assert.True(result.Succeeded);
        return user.Id;
    }
}
