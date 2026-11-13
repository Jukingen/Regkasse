using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Helpers;
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

public sealed class QuickUserGeneratorServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"QuickUserGen_{Guid.NewGuid():N}")
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

    private static QuickUserGeneratorService CreateService(AppDbContext db, UserManager<ApplicationUser> userManager) =>
        new(
            db,
            userManager,
            CreateUniquenessMock().Object,
            CreateUserCreationService(db, userManager));

    private static Mock<IUserUniquenessValidationService> CreateUniquenessMock(bool emailTaken = false)
    {
        var m = new Mock<IUserUniquenessValidationService>();
        m.Setup(x => x.IsEmailTakenByOtherUserAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(emailTaken);
        m.Setup(x => x.IsUserNameTakenByOtherUserAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(false);
        return m;
    }

    private static IUserCreationService CreateUserCreationService(AppDbContext db, UserManager<ApplicationUser> userManager) =>
        new UserCreationService(db, userManager, CreateUniquenessMock().Object);

    [Fact]
    public void GenerateSecurePassword_Returns_Requested_Length()
    {
        var password = PasswordGenerator.GenerateSecurePassword(12);
        Assert.Equal(12, password.Length);
    }

    [Fact]
    public void GenerateSecurePassword_Meets_Complexity_Rules()
    {
        var password = PasswordGenerator.GenerateSecurePassword(12);
        Assert.Matches(@"[A-Z]", password);
        Assert.Matches(@"[a-z]", password);
        Assert.Matches(@"\d", password);
        Assert.Matches(@"[!@#$%^&*()]", password);
    }

    [Fact]
    public async Task PrepareAsync_Allocates_Unique_Username()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe",
            Slug = "dev",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, CreateUserManager(db));
        var (plan, error) = await service.PrepareAsync(tenantId, Roles.Manager);

        Assert.Null(error);
        Assert.NotNull(plan);
        Assert.Matches(@"^manager\d+$", plan!.UserName);
    }

    [Fact]
    public async Task PrepareAsync_Returns_Rate_Limit_Error_After_Ten_Quick_Users_In_Hour()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Rate Cafe",
            Slug = "rate-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        var now = DateTime.UtcNow;
        for (var i = 0; i < QuickUserGeneratorService.MaxQuickUsersPerTenantPerHour; i++)
        {
            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SessionId = Guid.NewGuid().ToString(),
                UserId = "actor",
                UserRole = Roles.SuperAdmin,
                Action = AuditLogActions.TENANT_QUICK_USER_CREATED,
                EntityType = AuditLogEntityTypes.USER,
                Status = AuditLogStatus.Success,
                Timestamp = now.AddMinutes(-i),
                CreatedAt = now,
                Description = "test",
            });
        }

        await db.SaveChangesAsync();

        var service = CreateService(db, CreateUserManager(db));
        var (_, error) = await service.PrepareAsync(tenantId, Roles.Manager);

        Assert.NotNull(error);
        Assert.Contains("rate limit", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PrepareAsync_Allocates_Unique_Email_And_Password()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Test Cafe",
            Slug = "dev",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, CreateUserManager(db));
        var (plan, error) = await service.PrepareAsync(tenantId, Roles.Manager);

        Assert.Null(error);
        Assert.NotNull(plan);
        Assert.Matches(@"^manager_[a-z0-9]{6}@dev\.regkasse\.at$", plan!.Email);
        Assert.Equal(12, plan.Password.Length);
    }
}
