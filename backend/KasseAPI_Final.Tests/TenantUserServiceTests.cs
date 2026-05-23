using System.IO;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Helpers;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantUserServiceTests
{
    private static AppDbContext CreateDb(ICurrentTenantAccessor? tenantAccessor = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TenantUsers_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, tenantAccessor ?? NullCurrentTenantAccessor.Instance);
    }

    private static UserManager<ApplicationUser> CreateUserManager(AppDbContext db)
    {
        var store = new UserStore<ApplicationUser>(db);
        var identityOptions = Options.Create(new IdentityOptions());
        var userManager = new UserManager<ApplicationUser>(
            store,
            identityOptions,
            new PasswordHasher<ApplicationUser>(),
            new List<IUserValidator<ApplicationUser>>(),
            new List<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            Mock.Of<ILogger<UserManager<ApplicationUser>>>());

        var dataProtection = DataProtectionProvider.Create(
            new DirectoryInfo(Path.Combine(Path.GetTempPath(), "regkasse-tenant-user-tests")));
        userManager.RegisterTokenProvider(
            TokenOptions.DefaultProvider,
            new DataProtectorTokenProvider<ApplicationUser>(
                dataProtection,
                Options.Create(new DataProtectionTokenProviderOptions()),
                Mock.Of<ILogger<DataProtectorTokenProvider<ApplicationUser>>>()));

        return userManager;
    }

    private static TenantUserService CreateService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IUserUniquenessValidationService? uniqueness = null,
        IUserSessionInvalidation? sessionInvalidation = null,
        IQuickUserGeneratorService? quickUserGenerator = null,
        IAuditLogService? auditLog = null,
        ICurrentTenantAccessor? tenantAccessor = null) =>
        new(
            db,
            userManager,
            new UserTenantMembershipProvisioner(db),
            uniqueness ?? CreateUniquenessMock().Object,
            sessionInvalidation ?? Mock.Of<IUserSessionInvalidation>(),
            quickUserGenerator ?? new QuickUserGeneratorService(
                db,
                userManager,
                uniqueness ?? CreateUniquenessMock().Object),
            auditLog ?? Mock.Of<IAuditLogService>(),
            Mock.Of<IHttpContextAccessor>(),
            tenantAccessor ?? NullCurrentTenantAccessor.Instance,
            Mock.Of<ILogger<TenantUserService>>());

    private static Mock<IAuditLogService> CreateAuditMock()
    {
        var m = new Mock<IAuditLogService>();
        m.Setup(x => x.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>()))
            .ReturnsAsync(new AuditLog { Id = Guid.NewGuid() });
        m.Setup(x => x.LogUserLifecycleAsync(
                It.IsAny<AuditEventType>(),
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
            .ReturnsAsync(new AuditLog { Id = Guid.NewGuid() });
        return m;
    }

    private static Mock<IUserUniquenessValidationService> CreateUniquenessMock(bool emailTaken = false)
    {
        var m = new Mock<IUserUniquenessValidationService>();
        m.Setup(x => x.IsEmailTakenByOtherUserAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(emailTaken);
        return m;
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

    [Fact]
    public async Task ListAsync_ReturnsNull_When_Tenant_Missing()
    {
        await using var db = CreateDb();
        var service = CreateService(db, CreateUserManager(db));
        Assert.Null(await service.ListAsync(Guid.NewGuid()));
    }

    [Fact]
    public void GenerateRandomPassword_MeetsRules()
    {
        var password = PasswordGenerator.GenerateRandomPassword();
        Assert.True(password.Length >= 12);
        Assert.Matches(@"[A-Z]", password);
        Assert.Matches(@"[a-z]", password);
        Assert.Matches(@"\d", password);
        Assert.Matches(@"[!@#$%^&*()]", password);
    }

    [Fact]
    public async Task CreateAsync_Creates_User_Returns_Password_And_Logs_Audit()
    {
        await using var db = CreateDb();
        await SeedRolesAsync(db);
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Create Cafe",
            Slug = "create-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var audit = CreateAuditMock();
        var service = CreateService(db, CreateUserManager(db), auditLog: audit.Object);
        var actorId = Guid.NewGuid().ToString("D");
        var (result, error) = await service.CreateAsync(tenantId, new CreateTenantUserRequest
        {
            Email = "create@cafe.test",
            Role = Roles.Manager,
        }, actorId, Roles.SuperAdmin);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.False(string.IsNullOrEmpty(result.GeneratedPassword));
        Assert.True(result.ForcePasswordChangeOnNextLogin);
        Assert.Equal("create@cafe.test", result.Email);

        audit.Verify(
            x => x.LogUserLifecycleAsync(
                AuditEventType.UserCreated,
                actorId,
                Roles.SuperAdmin,
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                AuditLogStatus.Success,
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.Is<UserCreatedAuditDetails>(d =>
                    d.CreatedByUserId == actorId
                    && d.Role == Roles.Manager
                    && d.TenantId == tenantId
                    && d.PasswordReturned)),
            Times.Once);

        var created = await db.Users.AsNoTracking().SingleAsync(u => u.Email == "create@cafe.test");
        Assert.Null(created.TaxNumber);
    }

    [Fact]
    public async Task CreateAsync_Allows_Second_User_Without_Tax_Number()
    {
        await using var db = CreateDb();
        await SeedRolesAsync(db);
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Multi User Cafe",
            Slug = "multi-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, CreateUserManager(db));
        var actorId = Guid.NewGuid().ToString("D");

        var (first, firstError) = await service.CreateAsync(tenantId, new CreateTenantUserRequest
        {
            Email = "first@multi.test",
            Role = Roles.Cashier,
        }, actorId, Roles.SuperAdmin);
        var (second, secondError) = await service.CreateAsync(tenantId, new CreateTenantUserRequest
        {
            Email = "second@multi.test",
            Role = Roles.Cashier,
        }, actorId, Roles.SuperAdmin);

        Assert.Null(firstError);
        Assert.Null(secondError);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first!.Email, second!.Email);
    }

    [Fact]
    public async Task CreateQuickAsync_Generates_Email_Password_And_Logs_Quick_Audit()
    {
        await using var db = CreateDb();
        await SeedRolesAsync(db);
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Test Cafe",
            Slug = "cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, CreateUserManager(db));
        var actorId = Guid.NewGuid().ToString("D");
        var (result, error) = await service.CreateQuickAsync(tenantId, new CreateQuickTenantUserRequest
        {
            Role = Roles.Cashier,
        }, actorId);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.Matches(@"^cashier_[a-z0-9]{6}@cafe\.regkasse\.at$", result.Email);
        Assert.Equal(12, result.GeneratedPassword.Length);
        Assert.True(result.ForcePasswordChangeOnNextLogin);
        Assert.Equal(Roles.Cashier, result.Role);

        var auditRow = await db.AuditLogs.SingleAsync(a => a.Action == AuditLogActions.TENANT_QUICK_USER_CREATED);
        Assert.Equal(AuditLogEntityTypes.USER, auditRow.EntityType);
        Assert.Equal(tenantId, auditRow.TenantId);
        Assert.Equal(actorId, auditRow.UserId);
        Assert.Contains("Schnell-Benutzer", auditRow.Description);
        Assert.Contains("***HIDDEN***", auditRow.RequestData);
        Assert.Contains("quick_generate", auditRow.RequestData);
    }

    [Fact]
    public void QuickUserEmailGenerator_BuildEmail_Uses_Role_Prefix_And_Six_Char_Suffix()
    {
        var email = QuickUserEmailGenerator.BuildEmail("Manager", "cafe");
        Assert.Matches(@"^manager_[a-z0-9]{6}@cafe\.regkasse\.at$", email);
    }

    [Fact]
    public async Task CreateAsync_Succeeds_When_Ambient_Tenant_Differs_From_Target()
    {
        var tenantAccessor = new CurrentTenantAccessor { TenantId = LegacyDefaultTenantIds.Primary };
        await using var db = CreateDb(tenantAccessor);
        await SeedRolesAsync(db);
        var targetTenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = targetTenantId,
            Name = "Remote Cafe",
            Slug = "remote-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, CreateUserManager(db), tenantAccessor: tenantAccessor);
        var (result, error) = await service.CreateAsync(targetTenantId, new CreateTenantUserRequest
        {
            Email = "remote.manager@cafe.test",
            Role = Roles.Manager,
        }, "actor-1", Roles.Manager);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.Equal(
            targetTenantId,
            (await db.UserTenantMemberships.IgnoreQueryFilters().SingleAsync()).TenantId);
    }

    [Fact]
    public async Task CreateAsync_Rejects_Existing_Email()
    {
        await using var db = CreateDb();
        await SeedRolesAsync(db);
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Test Bar",
            Slug = "bar",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var userManager = CreateUserManager(db);
        var existing = new ApplicationUser
        {
            UserName = "existing@bar.test",
            Email = "existing@bar.test",
            FirstName = "E",
            LastName = "U",
            EmployeeNumber = "E2",
            Role = Roles.Cashier,
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
        };
        await userManager.CreateAsync(existing, "OldPass123!");
        await userManager.AddToRoleAsync(existing, Roles.Cashier);

        var service = CreateService(db, userManager);
        var (result, error) = await service.CreateAsync(tenantId, new CreateTenantUserRequest
        {
            Email = "existing@bar.test",
            Role = Roles.Manager,
        }, "actor-1", Roles.SuperAdmin);

        Assert.Null(result);
        Assert.Contains("already exists", error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResetPasswordAsync_Returns_Generated_Password_For_Tenant_User()
    {
        await using var db = CreateDb();
        await SeedRolesAsync(db);
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Pwd Cafe",
            Slug = "pwd-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        var userManager = CreateUserManager(db);
        var user = new ApplicationUser
        {
            UserName = "cashier@pwd.test",
            Email = "cashier@pwd.test",
            FirstName = "C",
            LastName = "U",
            EmployeeNumber = "E1",
            Role = Roles.Cashier,
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
        };
        await userManager.CreateAsync(user, "OldPass123!");
        await userManager.AddToRoleAsync(user, Roles.Cashier);
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = user.Id,
            TenantId = tenantId,
            IsActive = true,
            IsOwner = false,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, userManager);
        var (result, error) = await service.ResetPasswordAsync(tenantId, user.Id);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result!.GeneratedPassword));
    }
}
