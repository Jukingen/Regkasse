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
        IUserUniquenessValidationService? uniqueness = null) =>
        new(
            db,
            userManager,
            new UserTenantMembershipProvisioner(db),
            uniqueness ?? CreateUniquenessMock().Object,
            invitationEmail ?? CreateInvitationEmailMock(sent: false).Object,
            Mock.Of<ILogger<TenantUserService>>());

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
    public async Task InviteAsync_Creates_User_And_Assigns_Membership()
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

        var invitation = CreateInvitationEmailMock(sent: true);
        var service = CreateService(db, CreateUserManager(db), invitation.Object);
        var (result, error) = await service.InviteAsync(tenantId, new InviteTenantUserRequest
        {
            Email = "new.manager@cafe.test",
            Role = Roles.Manager,
        });

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.True(result!.UserCreated);
        Assert.True(result.InvitationEmailSent);
        Assert.Null(result.GeneratedPassword);

        var user = await db.Users.SingleAsync(u => u.Email == "new.manager@cafe.test");
        Assert.Equal(Roles.Manager, user.Role);
    }

    [Fact]
    public async Task InviteAsync_Returns_Password_When_Smtp_Not_Configured()
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

        var invitation = CreateInvitationEmailMock(sent: false, configured: false);
        var service = CreateService(db, CreateUserManager(db), invitation.Object);
        var (result, error) = await service.InviteAsync(tenantId, new InviteTenantUserRequest
        {
            Email = "invite@bar.test",
            Role = Roles.Cashier,
        });

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.True(result!.UserCreated);
        Assert.False(result.InvitationEmailSent);
        Assert.False(string.IsNullOrEmpty(result.GeneratedPassword));
    }
}
