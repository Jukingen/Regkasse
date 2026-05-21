using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Helpers;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Tenancy;
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
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TenantUsers_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
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

    private static TenantUserService CreateService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        ITenantInvitationEmailSender? invitationEmail = null,
        IUserUniquenessValidationService? uniqueness = null,
        IUserSessionInvalidation? sessionInvalidation = null,
        IQuickUserGeneratorService? quickUserGenerator = null,
        IAuditLogService? auditLog = null) =>
        new(
            db,
            userManager,
            new UserTenantMembershipProvisioner(db),
            uniqueness ?? CreateUniquenessMock().Object,
            invitationEmail ?? CreateInvitationEmailMock(sent: false).Object,
            sessionInvalidation ?? Mock.Of<IUserSessionInvalidation>(),
            quickUserGenerator ?? new QuickUserGeneratorService(
                db,
                userManager,
                uniqueness ?? CreateUniquenessMock().Object),
            auditLog ?? Mock.Of<IAuditLogService>(),
            Mock.Of<IHttpContextAccessor>(),
            NullCurrentTenantAccessor.Instance,
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
        return m;
    }

    private static Mock<IUserUniquenessValidationService> CreateUniquenessMock(bool emailTaken = false)
    {
        var m = new Mock<IUserUniquenessValidationService>();
        m.Setup(x => x.IsEmailTakenByOtherUserAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(emailTaken);
        return m;
    }

    private static Mock<ITenantInvitationEmailSender> CreateInvitationEmailMock(bool sent, bool configured = true)
    {
        var m = new Mock<ITenantInvitationEmailSender>();
        m.Setup(x => x.IsConfigured).Returns(configured);
        m.Setup(x => x.TrySendInvitationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sent);
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
        Assert.Matches(@"[!@#$%&*]", password);
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
        }, actorId);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.False(string.IsNullOrEmpty(result.GeneratedPassword));
        Assert.True(result.ForcePasswordChangeOnNextLogin);
        Assert.Equal("create@cafe.test", result.Email);

        audit.Verify(
            x => x.LogSystemOperationAsync(
                "TENANT_USER_CREATED",
                "TenantUser",
                actorId,
                Roles.SuperAdmin,
                It.IsAny<string?>(),
                It.Is<string?>(n => n != null && n.Contains("role=Manager")),
                AuditLogStatus.Success,
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            Times.Once);
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
    public async Task InviteAsync_Creates_User_Assigns_Membership_And_Returns_Password()
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
        var (result, error) = await service.InviteAsync(tenantId, new InviteTenantUserRequest
        {
            Email = "new.manager@cafe.test",
            Role = Roles.Manager,
        }, "actor-1");

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.True(result!.UserCreated);
        Assert.False(result.InvitationEmailSent);
        Assert.False(string.IsNullOrEmpty(result.GeneratedPassword));
        Assert.True(result.ForcePasswordChangeOnNextLogin);
        Assert.Equal("https://cafe.regkasse.at", result.TenantPortalUrl);

        var user = await db.Users.SingleAsync(u => u.Email == "new.manager@cafe.test");
        Assert.Equal(Roles.Manager, user.Role);
        Assert.True(user.MustChangePasswordOnNextLogin);
    }

    [Fact]
    public async Task InviteAsync_Assigns_Existing_User_Without_Password()
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
            TaxNumber = string.Empty,
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
        };
        await userManager.CreateAsync(existing, "OldPass123!");
        await userManager.AddToRoleAsync(existing, Roles.Cashier);

        var service = CreateService(db, userManager);
        var (result, error) = await service.InviteAsync(tenantId, new InviteTenantUserRequest
        {
            Email = "existing@bar.test",
            Role = Roles.Manager,
        }, "actor-1");

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.False(result!.UserCreated);
        Assert.Null(result.GeneratedPassword);
        Assert.False(result.ForcePasswordChangeOnNextLogin);
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
            TaxNumber = string.Empty,
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
