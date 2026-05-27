using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class UserUniquenessValidationServiceTests
{
    [Fact]
    public async Task IsUserNameTakenByOtherUserAsync_Is_Case_Insensitive()
    {
        await using var db = CreateDb();
        var userManager = CreateUserManager(db);
        db.Users.Add(new ApplicationUser
        {
            Id = Guid.NewGuid().ToString("D"),
            UserName = "Admin",
            NormalizedUserName = "ADMIN",
            Email = "admin@test.com",
            NormalizedEmail = "ADMIN@TEST.COM",
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var service = new UserUniquenessValidationService(userManager);
        var taken = await service.IsUserNameTakenByOtherUserAsync("admin", excludeUserId: null);

        Assert.True(taken);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"uniqueness-svc-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static UserManager<ApplicationUser> CreateUserManager(AppDbContext db)
    {
        var store = new UserStore<ApplicationUser>(db);
        var userManager = new UserManager<ApplicationUser>(
            store,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            new List<IUserValidator<ApplicationUser>>(),
            new List<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            Mock.Of<ILogger<UserManager<ApplicationUser>>>());

        userManager.RegisterTokenProvider(
            TokenOptions.DefaultProvider,
            new DataProtectorTokenProvider<ApplicationUser>(
                DataProtectionProvider.Create(Guid.NewGuid().ToString("N")),
                Options.Create(new DataProtectionTokenProviderOptions()),
                Mock.Of<ILogger<DataProtectorTokenProvider<ApplicationUser>>>()));

        return userManager;
    }
}
