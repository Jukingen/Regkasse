using KasseAPI_Final.Data;
using KasseAPI_Final.Helpers;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests;

public class IdentityLoginLookupTests
{
    private static (AppDbContext Db, UserManager<ApplicationUser> UserManager) CreateUserManager()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o =>
            o.UseInMemoryDatabase($"identity-login-{Guid.NewGuid():N}"));
        services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return (db, userManager);
    }

    [Fact]
    public async Task FindByLoginIdentifierAsync_Username_Is_Case_Insensitive()
    {
        var (db, userManager) = CreateUserManager();
        db.Users.Add(new ApplicationUser
        {
            Id = Guid.NewGuid().ToString("D"),
            UserName = "Mustafa",
            NormalizedUserName = "MUSTAFA",
            Email = "mustafa@test.com",
            NormalizedEmail = "MUSTAFA@TEST.COM",
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var found = await IdentityLoginLookup.FindByLoginIdentifierAsync(userManager, "mustafa");
        Assert.NotNull(found);
        Assert.Equal("Mustafa", found!.UserName);
    }

    [Fact]
    public async Task IsUserNameTakenByOtherUserAsync_Treats_Admin_And_admin_As_Duplicate()
    {
        var (db, userManager) = CreateUserManager();
        var existingId = Guid.NewGuid().ToString("D");
        db.Users.Add(new ApplicationUser
        {
            Id = existingId,
            UserName = "Admin",
            NormalizedUserName = "ADMIN",
            Email = "admin@test.com",
            NormalizedEmail = "ADMIN@TEST.COM",
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var taken = await IdentityLoginLookup.IsUserNameTakenByOtherUserAsync(
            userManager,
            "admin",
            excludeUserId: null);

        Assert.True(taken);
    }

    [Fact]
    public async Task IsUserNameTakenByOtherUserAsync_Excludes_Current_User_On_Update()
    {
        var (db, userManager) = CreateUserManager();
        var userId = Guid.NewGuid().ToString("D");
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = "Mustafa",
            NormalizedUserName = "MUSTAFA",
            Email = "mustafa@test.com",
            NormalizedEmail = "MUSTAFA@TEST.COM",
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var taken = await IdentityLoginLookup.IsUserNameTakenByOtherUserAsync(
            userManager,
            "MUSTAFA",
            excludeUserId: userId);

        Assert.False(taken);
    }
}
