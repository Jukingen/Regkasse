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
    public async Task ChangeUserRoleAsync_UpdatesIdentityRole_AndInvalidatesSessions()
    {
        await using var db = CreateDb();
        await SeedRolesAsync(db);
        var userManager = CreateUserManager(db);
        var user = await CreateUserAsync(userManager, Roles.Manager);

        var sessionInvalidation = new Mock<IUserSessionInvalidation>();
        var service = CreateService(db, userManager, sessionInvalidation: sessionInvalidation);
        var (result, error) = await service.ChangeUserRoleAsync(
            user,
            Roles.Cashier,
            actorUserId: "actor-1",
            actorRole: Roles.SuperAdmin,
            tenantIdForAudit: null);

        Assert.Null(error);
        Assert.True(result.RoleChanged);
        Assert.Equal(Roles.Manager, result.PreviousRole);
        Assert.Equal(Roles.Cashier, result.NewRole);
        Assert.Equal(Roles.Cashier, user.Role);
        sessionInvalidation.Verify(x => x.InvalidateSessionsForUserAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangeUserRoleAsync_SameRole_ReturnsWithoutChange()
    {
        await using var db = CreateDb();
        await SeedRolesAsync(db);
        var userManager = CreateUserManager(db);
        var user = await CreateUserAsync(userManager, Roles.Manager);

        var service = CreateService(db, userManager);
        var (result, error) = await service.ChangeUserRoleAsync(
            user,
            Roles.Manager,
            actorUserId: "actor-1",
            actorRole: Roles.SuperAdmin,
            tenantIdForAudit: null);

        Assert.Null(error);
        Assert.False(result.RoleChanged);
    }

    [Fact]
    public async Task UpdateRoleAsync_UsesTenantEndpointPath()
    {
        await using var db = CreateDb();
        await SeedRolesAsync(db);
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Role Cafe",
            Slug = "role-cafe",
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
            new UpdateTenantUserRoleRequest { Role = Roles.Cashier });

        Assert.Null(error);
        Assert.NotNull(dto);
        Assert.Equal(Roles.Cashier, dto!.Role);
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
            It.IsAny<object?>(),
            It.IsAny<UserCreatedAuditDetails?>()), Times.Once);
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

    private static UserRoleChangeService CreateService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        Mock<IAuditLogService>? audit = null,
        Mock<IUserSessionInvalidation>? sessionInvalidation = null)
    {
        return new UserRoleChangeService(
            userManager,
            (audit ?? CreateAuditMock()).Object,
            (sessionInvalidation ?? new Mock<IUserSessionInvalidation>()).Object,
            Mock.Of<ILogger<UserRoleChangeService>>());
    }

    private static TenantUserService CreateTenantUserService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        Mock<IAuditLogService>? audit = null)
    {
        var auditMock = audit ?? CreateAuditMock();
        var roleChange = new UserRoleChangeService(
            userManager,
            auditMock.Object,
            Mock.Of<IUserSessionInvalidation>(),
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
