using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Helpers;
using KasseAPI_Final.Models;
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

public sealed class UsernameChangeRateLimitTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"UsernameRate_{Guid.NewGuid():N}")
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
    public async Task GetRateLimitErrorAsync_Returns_Null_When_No_Prior_Change()
    {
        await using var db = CreateDb();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString("D"),
            UserName = "cashier1",
            Email = "c@test.com",
            Role = Roles.Cashier,
            IsActive = true,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var userManager = CreateUserManager(db);
        var error = await UsernameChangeRateLimit.GetRateLimitErrorAsync(userManager, user);

        Assert.Null(error);
    }

    [Fact]
    public async Task GetRateLimitErrorAsync_Blocks_When_Changed_Within_Interval()
    {
        await using var db = CreateDb();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString("D"),
            UserName = "cashier1",
            Email = "c@test.com",
            Role = Roles.Cashier,
            IsActive = true,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var userManager = CreateUserManager(db);
        await userManager.AddClaimAsync(
            user,
            new Claim(UsernameChangeRateLimit.LastChangeClaimType, DateTime.UtcNow.AddDays(-2).ToString("O")));

        var error = await UsernameChangeRateLimit.GetRateLimitErrorAsync(userManager, user);

        Assert.NotNull(error);
        Assert.Contains("7 days", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecordChangeAsync_Upserts_Claim()
    {
        await using var db = CreateDb();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString("D"),
            UserName = "cashier1",
            Email = "c@test.com",
            Role = Roles.Cashier,
            IsActive = true,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var userManager = CreateUserManager(db);
        await UsernameChangeRateLimit.RecordChangeAsync(userManager, user);
        await UsernameChangeRateLimit.RecordChangeAsync(userManager, user);

        var claims = await userManager.GetClaimsAsync(user);
        Assert.Single(claims, c => c.Type == UsernameChangeRateLimit.LastChangeClaimType);
    }
}
