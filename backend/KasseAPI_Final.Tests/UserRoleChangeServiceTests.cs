using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class UserRoleChangeServiceTests
{
    [Fact]
    public async Task ChangeUserRoleAsync_PreserveTrue_CreatesGrantOverrides_ForPermissionsNotInNewRole()
    {
        await using var db = CreateDb();
        await SeedRolesAsync(db);
        var userManager = CreateUserManager(db);
        var user = await CreateUserAsync(userManager, Roles.Manager);

        var service = CreateService(db, userManager);
        var (result, error) = await service.ChangeUserRoleAsync(
            user,
            Roles.Cashier,
            preservePreviousPermissions: true,
            actorUserId: "actor-1",
            actorRole: Roles.SuperAdmin,
            tenantIdForAudit: null);

        Assert.Null(error);
        Assert.True(result.RoleChanged);
        Assert.Equal(Roles.Manager, result.PreviousRole);
        Assert.Equal(Roles.Cashier, result.NewRole);
        Assert.True(result.PreservePreviousPermissions);
        Assert.True(result.OverridesCreatedOrUpdated > 0);

        var managerOnly = RolePermissionMatrix.GetPermissionsForRole(Roles.Manager)
            .Except(RolePermissionMatrix.GetPermissionsForRole(Roles.Cashier))
            .ToList();
        Assert.NotEmpty(managerOnly);

        var overrides = await db.UserPermissionOverrides
            .Where(o => o.UserId == user.Id && o.IsGranted)
            .Select(o => o.Permission)
            .ToListAsync();
        Assert.Contains(AppPermissions.AuditExport, overrides);
        Assert.Contains(AppPermissions.UserView, overrides);
    }

    [Fact]
    public async Task ChangeUserRoleAsync_PreserveFalse_DoesNotCreateOverrides()
    {
        await using var db = CreateDb();
        await SeedRolesAsync(db);
        var userManager = CreateUserManager(db);
        var user = await CreateUserAsync(userManager, Roles.Manager);

        var service = CreateService(db, userManager);
        var (result, error) = await service.ChangeUserRoleAsync(
            user,
            Roles.Cashier,
            preservePreviousPermissions: false,
            actorUserId: "actor-1",
            actorRole: Roles.SuperAdmin,
            tenantIdForAudit: null);

        Assert.Null(error);
        Assert.True(result.RoleChanged);
        Assert.Equal(0, result.OverridesCreatedOrUpdated);
        Assert.Empty(await db.UserPermissionOverrides.Where(o => o.UserId == user.Id).ToListAsync());
    }

    [Fact]
    public async Task UpdateRoleAsync_PreserveTrue_UsesTenantEndpointPath()
    {
        await using var db = CreateDb();
        await SeedRolesAsync(db);
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Preserve Cafe",
            Slug = "preserve-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        var userManager = CreateUserManager(db);
        var user = await CreateUserAsync(userManager, Roles.Manager);
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = user.Id,
            TenantId = tenantId,
            IsActive = true,
            IsOwner = false,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var audit = CreateAuditMock();
        var tenantService = CreateTenantUserService(db, userManager, audit: audit);
        var (dto, error) = await tenantService.UpdateRoleAsync(
            tenantId,
            user.Id,
            new UpdateTenantUserRoleRequest
            {
                Role = Roles.Cashier,
                PreservePreviousPermissions = true,
            });

        Assert.Null(error);
        Assert.NotNull(dto);
        Assert.Equal(Roles.Cashier, dto!.Role);
        Assert.True(await db.UserPermissionOverrides.AnyAsync(o => o.UserId == user.Id && o.IsGranted));
        audit.Verify(x => x.LogUserLifecycleAsync(
            AuditEventType.UserRoleChanged,
            It.IsAny<string>(),
            It.IsAny<string>(),
            user.Id,
            tenantId,
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            AuditLogStatus.Success,
            It.IsAny<string?>(),
            It.IsAny<object?>(),
            It.Is<object?>(v => v != null),
            It.IsAny<UserCreatedAuditDetails?>()), Times.Once);
    }

    [Fact]
    public async Task ChangeUserRoleAsync_SuperAdminPreviousRole_IgnoresPreserveFlag()
    {
        await using var db = CreateDb();
        await SeedRolesAsync(db);
        var userManager = CreateUserManager(db);
        var user = await CreateUserAsync(userManager, Roles.SuperAdmin);

        var service = CreateService(db, userManager);
        var (result, error) = await service.ChangeUserRoleAsync(
            user,
            Roles.Manager,
            preservePreviousPermissions: true,
            actorUserId: "actor-1",
            actorRole: Roles.SuperAdmin,
            tenantIdForAudit: null);

        Assert.Null(error);
        Assert.True(result.RoleChanged);
        Assert.False(result.PreservePreviousPermissions);
        Assert.Equal(0, result.OverridesCreatedOrUpdated);
        Assert.Empty(await db.UserPermissionOverrides.Where(o => o.UserId == user.Id).ToListAsync());
    }

    [Fact]
    public async Task ChangeUserRoleAsync_CustomRole_PreservesCustomPermissionClaims()
    {
        const string customRole = "CustomAuditor";
        await using var db = CreateDb();
        await SeedRolesAsync(db);
        var roleManager = CreateRoleManager(db);
        await roleManager.CreateAsync(new IdentityRole(customRole));
        await roleManager.AddClaimAsync(
            (await roleManager.FindByNameAsync(customRole))!,
            new System.Security.Claims.Claim("permission", AppPermissions.AuditCleanup));

        var userManager = CreateUserManager(db);
        var user = await CreateUserAsync(userManager, customRole);

        var service = CreateService(db, userManager);
        var (_, error) = await service.ChangeUserRoleAsync(
            user,
            Roles.Cashier,
            preservePreviousPermissions: true,
            actorUserId: "actor-1",
            actorRole: Roles.SuperAdmin,
            tenantIdForAudit: null);

        Assert.Null(error);
        var overrides = await db.UserPermissionOverrides
            .Where(o => o.UserId == user.Id && o.IsGranted)
            .Select(o => o.Permission)
            .ToListAsync();
        Assert.Contains(AppPermissions.AuditCleanup, overrides);
    }

    [Fact]
    public async Task ChangeUserRoleAsync_ManagerToCustomRole_PreserveTrue_CreatesOverridesForManagerOnlyPermissions()
    {
        const string customRole = "CustomBarStaff";
        await using var db = CreateDb();
        await SeedRolesAsync(db);
        var roleManager = CreateRoleManager(db);
        await roleManager.CreateAsync(new IdentityRole(customRole));
        await roleManager.AddClaimAsync(
            (await roleManager.FindByNameAsync(customRole))!,
            new System.Security.Claims.Claim("permission", AppPermissions.ReportView));

        var userManager = CreateUserManager(db);
        var user = await CreateUserAsync(userManager, Roles.Manager);

        var service = CreateService(db, userManager);
        var (result, error) = await service.ChangeUserRoleAsync(
            user,
            customRole,
            preservePreviousPermissions: true,
            actorUserId: "actor-1",
            actorRole: Roles.SuperAdmin,
            tenantIdForAudit: null);

        Assert.Null(error);
        Assert.True(result.RoleChanged);
        Assert.True(result.PreservePreviousPermissions);
        Assert.True(result.OverridesCreatedOrUpdated > 0);

        var customDefaults = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { AppPermissions.ReportView };
        var managerOnly = RolePermissionMatrix.GetPermissionsForRole(Roles.Manager)
            .Where(p => !customDefaults.Contains(p))
            .ToList();
        Assert.NotEmpty(managerOnly);

        var overrides = await db.UserPermissionOverrides
            .Where(o => o.UserId == user.Id && o.IsGranted)
            .Select(o => o.Permission)
            .ToListAsync();
        Assert.Contains(AppPermissions.AuditExport, overrides);
        Assert.DoesNotContain(AppPermissions.ReportView, overrides);
    }

    [Fact]
    public async Task ChangeUserRoleAsync_ManagerToCustomRole_PreserveFalse_DoesNotCreateOverrides()
    {
        const string customRole = "CustomBarStaff";
        await using var db = CreateDb();
        await SeedRolesAsync(db);
        var roleManager = CreateRoleManager(db);
        await roleManager.CreateAsync(new IdentityRole(customRole));
        await roleManager.AddClaimAsync(
            (await roleManager.FindByNameAsync(customRole))!,
            new System.Security.Claims.Claim("permission", AppPermissions.ReportView));

        var userManager = CreateUserManager(db);
        var user = await CreateUserAsync(userManager, Roles.Manager);

        var service = CreateService(db, userManager);
        var (result, error) = await service.ChangeUserRoleAsync(
            user,
            customRole,
            preservePreviousPermissions: false,
            actorUserId: "actor-1",
            actorRole: Roles.SuperAdmin,
            tenantIdForAudit: null);

        Assert.Null(error);
        Assert.True(result.RoleChanged);
        Assert.False(result.PreservePreviousPermissions);
        Assert.Equal(0, result.OverridesCreatedOrUpdated);
        Assert.Empty(await db.UserPermissionOverrides.Where(o => o.UserId == user.Id).ToListAsync());
    }

    [Fact]
    public async Task ChangeUserRoleAsync_NoPreviousRole_IgnoresPreserveFlag()
    {
        await using var db = CreateDb();
        await SeedRolesAsync(db);
        var userManager = CreateUserManager(db);
        var user = new ApplicationUser
        {
            UserName = $"user-no-role-{Guid.NewGuid():N}@test.local",
            Email = $"user-no-role-{Guid.NewGuid():N}@test.local",
            FirstName = "Test",
            LastName = "User",
            EmployeeNumber = Guid.NewGuid().ToString("N")[..8],
            Role = "",
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
        };
        await userManager.CreateAsync(user, "TestPass123!");

        var service = CreateService(db, userManager);
        var (result, error) = await service.ChangeUserRoleAsync(
            user,
            Roles.Cashier,
            preservePreviousPermissions: true,
            actorUserId: "actor-1",
            actorRole: Roles.SuperAdmin,
            tenantIdForAudit: null);

        Assert.Null(error);
        Assert.True(result.RoleChanged);
        Assert.False(result.PreservePreviousPermissions);
        Assert.Equal(0, result.OverridesCreatedOrUpdated);
        Assert.Equal(Roles.Cashier, user.Role);
    }

    [Fact]
    public async Task ChangeUserRoleAsync_CustomRoleToSystemRole_PreservesCustomOnlyPermissions()
    {
        const string customRole = "CustomAuditor";
        await using var db = CreateDb();
        await SeedRolesAsync(db);
        var roleManager = CreateRoleManager(db);
        await roleManager.CreateAsync(new IdentityRole(customRole));
        await roleManager.AddClaimAsync(
            (await roleManager.FindByNameAsync(customRole))!,
            new System.Security.Claims.Claim("permission", AppPermissions.AuditCleanup));

        var userManager = CreateUserManager(db);
        var user = await CreateUserAsync(userManager, customRole);

        var service = CreateService(db, userManager);
        var (_, error) = await service.ChangeUserRoleAsync(
            user,
            Roles.Manager,
            preservePreviousPermissions: true,
            actorUserId: "actor-1",
            actorRole: Roles.SuperAdmin,
            tenantIdForAudit: null);

        Assert.Null(error);
        var overrides = await db.UserPermissionOverrides
            .Where(o => o.UserId == user.Id && o.IsGranted)
            .Select(o => o.Permission)
            .ToListAsync();
        Assert.Contains(AppPermissions.AuditCleanup, overrides);
        Assert.DoesNotContain(
            AppPermissions.AuditExport,
            overrides);
    }

    [Fact]
    public async Task ChangeUserRoleAsync_LogsAudit_WithOldRoleNewRoleAndPreserveFlag()
    {
        await using var db = CreateDb();
        await SeedRolesAsync(db);
        var userManager = CreateUserManager(db);
        var user = await CreateUserAsync(userManager, Roles.Manager);
        var audit = CreateAuditMock();
        object? capturedOldValues = null;
        object? capturedNewValues = null;
        audit.Setup(x => x.LogUserLifecycleAsync(
                AuditEventType.UserRoleChanged,
                It.IsAny<string>(),
                It.IsAny<string>(),
                user.Id,
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                AuditLogStatus.Success,
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<UserCreatedAuditDetails?>()))
            .Callback<AuditEventType, string, string, string, Guid?, string?, string?, AuditLogStatus, string?, object?, object?, UserCreatedAuditDetails?>(
                (_, _, _, _, _, _, _, _, _, oldValues, newValues, _) =>
                {
                    capturedOldValues = oldValues;
                    capturedNewValues = newValues;
                })
            .ReturnsAsync(new AuditLog { Id = Guid.NewGuid() });

        var service = new UserRoleChangeService(
            userManager,
            new RolePermissionResolver(CreateRoleManager(db)),
            new UserPermissionOverrideService(
                db,
                new RolePermissionResolver(CreateRoleManager(db)),
                new EffectivePermissionResolver(db, new RolePermissionResolver(CreateRoleManager(db)))),
            audit.Object,
            Mock.Of<IUserSessionInvalidation>(),
            db,
            Mock.Of<ILogger<UserRoleChangeService>>());

        var (_, error) = await service.ChangeUserRoleAsync(
            user,
            Roles.Cashier,
            preservePreviousPermissions: true,
            actorUserId: "actor-1",
            actorRole: Roles.SuperAdmin,
            tenantIdForAudit: Guid.NewGuid());

        Assert.Null(error);
        Assert.NotNull(capturedOldValues);
        Assert.NotNull(capturedNewValues);

        var oldJson = System.Text.Json.JsonSerializer.Serialize(capturedOldValues);
        var newJson = System.Text.Json.JsonSerializer.Serialize(capturedNewValues);
        Assert.Contains(Roles.Manager, oldJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Roles.Cashier, newJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("preservedPermissions", newJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("true", newJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChangeUserRoleAsync_OverlappingPermissions_DoesNotDuplicateOverrides()
    {
        await using var db = CreateDb();
        await SeedRolesAsync(db);
        var userManager = CreateUserManager(db);
        var user = await CreateUserAsync(userManager, Roles.Manager);

        var sharedPermission = AppPermissions.ReportView;
        Assert.Contains(sharedPermission, RolePermissionMatrix.GetPermissionsForRole(Roles.Manager));
        Assert.Contains(sharedPermission, RolePermissionMatrix.GetPermissionsForRole(Roles.Accountant));

        var service = CreateService(db, userManager);
        var (result, error) = await service.ChangeUserRoleAsync(
            user,
            Roles.Accountant,
            preservePreviousPermissions: true,
            actorUserId: "actor-1",
            actorRole: Roles.SuperAdmin,
            tenantIdForAudit: null);

        Assert.Null(error);
        Assert.DoesNotContain(
            AppPermissions.ReportView,
            await db.UserPermissionOverrides.Where(o => o.UserId == user.Id).Select(o => o.Permission).ToListAsync());
        Assert.True(result.OverridesCreatedOrUpdated >= 0);
    }

    private static async Task<ApplicationUser> CreateUserAsync(UserManager<ApplicationUser> userManager, string role)
    {
        var user = new ApplicationUser
        {
            UserName = $"user-{role}-{Guid.NewGuid():N}@test.local",
            Email = $"user-{role}-{Guid.NewGuid():N}@test.local",
            FirstName = "Test",
            LastName = "User",
            EmployeeNumber = Guid.NewGuid().ToString("N")[..8],
            Role = role,
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
        };
        await userManager.CreateAsync(user, "TestPass123!");
        await userManager.AddToRoleAsync(user, role);
        return user;
    }

    private static UserRoleChangeService CreateService(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        var roleManager = CreateRoleManager(db);
        return new UserRoleChangeService(
            userManager,
            new RolePermissionResolver(roleManager),
            new UserPermissionOverrideService(
                db,
                new RolePermissionResolver(roleManager),
                new EffectivePermissionResolver(db, new RolePermissionResolver(roleManager))),
            CreateAuditMock().Object,
            Mock.Of<IUserSessionInvalidation>(),
            db,
            Mock.Of<ILogger<UserRoleChangeService>>());
    }

    private static TenantUserService CreateTenantUserService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        Mock<IAuditLogService>? audit = null)
    {
        var auditMock = audit ?? CreateAuditMock();
        var roleManager = CreateRoleManager(db);
        var roleChange = new UserRoleChangeService(
            userManager,
            new RolePermissionResolver(roleManager),
            new UserPermissionOverrideService(
                db,
                new RolePermissionResolver(roleManager),
                new EffectivePermissionResolver(db, new RolePermissionResolver(roleManager))),
            auditMock.Object,
            Mock.Of<IUserSessionInvalidation>(),
            db,
            Mock.Of<ILogger<UserRoleChangeService>>());

        return new TenantUserService(
            db,
            userManager,
            new UserTenantMembershipProvisioner(db),
            Mock.Of<IUserUniquenessValidationService>(),
            Mock.Of<IUserSessionInvalidation>(),
            Mock.Of<IQuickUserGeneratorService>(),
            Mock.Of<IUserCreationService>(),
            auditMock.Object,
            Mock.Of<Microsoft.AspNetCore.Http.IHttpContextAccessor>(),
            NullCurrentTenantAccessor.Instance,
            ActivityEventTestSupport.CreateRecorder(),
            roleChange,
            Mock.Of<ILogger<TenantUserService>>());
    }

    private static Mock<IAuditLogService> CreateAuditMock()
    {
        var m = new Mock<IAuditLogService>();
        m.Setup(x => x.LogUserLifecycleAsync(
                It.IsAny<AuditEventType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<UserCreatedAuditDetails?>()))
            .ReturnsAsync(new AuditLog { Id = Guid.NewGuid() });
        return m;
    }

    private static RoleManager<IdentityRole> CreateRoleManager(AppDbContext db)
    {
        var store = new RoleStore<IdentityRole>(db);
        return new RoleManager<IdentityRole>(
            store,
            Array.Empty<IRoleValidator<IdentityRole>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            Mock.Of<ILogger<RoleManager<IdentityRole>>>());
    }

    private static UserManager<ApplicationUser> CreateUserManager(AppDbContext db)
    {
        var store = new UserStore<ApplicationUser>(db);
        return new UserManager<ApplicationUser>(
            store,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            new List<IUserValidator<ApplicationUser>>(),
            new List<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            Mock.Of<ILogger<UserManager<ApplicationUser>>>());
    }

    private static async Task SeedRolesAsync(AppDbContext db)
    {
        foreach (var role in Roles.Canonical)
        {
            db.Roles.Add(new IdentityRole
            {
                Id = Guid.NewGuid().ToString("D"),
                Name = role,
                NormalizedName = role.ToUpperInvariant(),
            });
        }

        await db.SaveChangesAsync();
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"UserRoleChange_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }
}
