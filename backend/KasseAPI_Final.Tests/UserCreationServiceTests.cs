using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
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

public sealed class UserCreationServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"UserCreation_{Guid.NewGuid():N}")
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

    [Fact]
    public async Task ResolveUsernameAsync_Generates_When_Not_Provided()
    {
        await using var db = CreateDb();
        var userManager = CreateUserManager(db);
        var service = new UserCreationService(db, userManager, new UserUniquenessValidationService(userManager));

        var (userName, error) = await service.ResolveUsernameAsync(null, Roles.Manager);

        Assert.Null(error);
        Assert.Equal("manager1", userName);
    }

    [Fact]
    public async Task ResolveUsernameAsync_Rejects_Taken_Explicit_Name()
    {
        await using var db = CreateDb();
        var userManager = CreateUserManager(db);
        db.Users.Add(new ApplicationUser
        {
            Id = Guid.NewGuid().ToString("D"),
            UserName = "cashier1",
            NormalizedUserName = "CASHIER1",
            Email = "taken@test.com",
            Role = Roles.Cashier,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var service = new UserCreationService(db, userManager, new UserUniquenessValidationService(userManager));
        var (_, error) = await service.ResolveUsernameAsync("cashier1", Roles.Cashier);

        Assert.NotNull(error);
        Assert.Contains("already taken", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveUsernameAsync_Rejects_Reserved_Explicit_Name()
    {
        await using var db = CreateDb();
        var userManager = CreateUserManager(db);
        var service = new UserCreationService(db, userManager, new UserUniquenessValidationService(userManager));

        var (_, error) = await service.ResolveUsernameAsync("support", Roles.Manager);

        Assert.NotNull(error);
        Assert.Contains("reserved", error, StringComparison.OrdinalIgnoreCase);
    }
}
