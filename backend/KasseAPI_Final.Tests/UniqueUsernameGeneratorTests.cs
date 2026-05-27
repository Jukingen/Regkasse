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

public sealed class UniqueUsernameGeneratorTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"UniqueUsername_{Guid.NewGuid():N}")
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

    [Theory]
    [InlineData("SuperAdmin", "admin")]
    [InlineData("Manager", "manager")]
    [InlineData("Cashier", "cashier")]
    [InlineData("Accountant", "user")]
    [InlineData(null, "user")]
    public void GetRolePrefix_Maps_Roles(string? role, string expectedPrefix)
    {
        Assert.Equal(expectedPrefix, UniqueUsernameGenerator.GetRolePrefix(role));
    }

    [Theory]
    [InlineData("user1", "user", 1)]
    [InlineData("user42", "user", 42)]
    [InlineData("manager3", "manager", 3)]
    [InlineData("manager3_abc", "manager", 3)]
    [InlineData("user", "user", 0)]
    [InlineData("other1", "user", 0)]
    public void ParseNumericSuffix_Parses_Expected_Number(string userName, string prefix, int expected)
    {
        Assert.Equal(expected, UniqueUsernameGenerator.ParseNumericSuffix(userName, prefix));
    }

    [Fact]
    public async Task AllocateUniqueUsernameAsync_Returns_Incremental_Name()
    {
        await using var db = CreateDb();
        db.Users.Add(new ApplicationUser
        {
            Id = Guid.NewGuid().ToString("D"),
            UserName = "cashier1",
            NormalizedUserName = "CASHIER1",
            Email = "c1@test.com",
            Role = Roles.Cashier,
            IsActive = true,
        });
        db.Users.Add(new ApplicationUser
        {
            Id = Guid.NewGuid().ToString("D"),
            UserName = "cashier2",
            NormalizedUserName = "CASHIER2",
            Email = "c2@test.com",
            Role = Roles.Cashier,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var userManager = CreateUserManager(db);
        var userName = await UniqueUsernameGenerator.AllocateUniqueUsernameAsync(db, userManager, Roles.Cashier);

        Assert.Equal("cashier3", userName);
    }

    [Fact]
    public async Task GenerateNextUsernameAsync_Starts_At_One_When_No_Collision()
    {
        await using var db = CreateDb();
        var userName = await UniqueUsernameGenerator.GenerateNextUsernameAsync(db, Roles.Manager);

        Assert.Equal("manager1", userName);
    }

    [Fact]
    public async Task GetSuggestionAsync_Suggests_Next_Number_And_Lists_Available_Suffixes()
    {
        await using var db = CreateDb();
        db.Users.Add(new ApplicationUser
        {
            Id = Guid.NewGuid().ToString("D"),
            UserName = "manager1",
            NormalizedUserName = "MANAGER1",
            Email = "m1@test.com",
            Role = Roles.Manager,
            IsActive = true,
        });
        db.Users.Add(new ApplicationUser
        {
            Id = Guid.NewGuid().ToString("D"),
            UserName = "manager2",
            NormalizedUserName = "MANAGER2",
            Email = "m2@test.com",
            Role = Roles.Manager,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var (suggested, available) = await UniqueUsernameGenerator.GetSuggestionAsync(db, Roles.Manager);

        Assert.Equal("manager3", suggested);
        Assert.Equal(new[] { 3, 4, 5 }, available);
    }

    [Fact]
    public async Task GetSuggestionAsync_When_Gap_Exists_Still_Uses_Max_Plus_One_For_Suggestion()
    {
        await using var db = CreateDb();
        db.Users.Add(new ApplicationUser
        {
            Id = Guid.NewGuid().ToString("D"),
            UserName = "manager1",
            NormalizedUserName = "MANAGER1",
            Email = "m1@test.com",
            Role = Roles.Manager,
            IsActive = true,
        });
        db.Users.Add(new ApplicationUser
        {
            Id = Guid.NewGuid().ToString("D"),
            UserName = "manager4",
            NormalizedUserName = "MANAGER4",
            Email = "m4@test.com",
            Role = Roles.Manager,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var (suggested, available) = await UniqueUsernameGenerator.GetSuggestionAsync(db, Roles.Manager);

        Assert.Equal("manager5", suggested);
        Assert.Equal(new[] { 5, 6, 7 }, available);
    }
}
