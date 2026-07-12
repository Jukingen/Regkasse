using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class UserSeedDataProductionGuardTests
{
    private static UserManager<ApplicationUser> CreateUserManager(AppDbContext db)
    {
        var store = new UserStore<ApplicationUser>(db);
        return new UserManager<ApplicationUser>(
            store,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            [],
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            Mock.Of<ILogger<UserManager<ApplicationUser>>>());
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"UserSeedGuard_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static IHostEnvironment CreateEnvironment(string name)
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(name);
        return env.Object;
    }

    [Fact]
    public async Task WarnIfDefaultSeedUsersExistInProductionAsync_WhenDefaultAdminExists_LogsWarning()
    {
        await using var db = CreateDb();
        var userManager = CreateUserManager(db);
        await userManager.CreateAsync(new ApplicationUser
        {
            UserName = UserSeedData.DefaultAdminEmail,
            Email = UserSeedData.DefaultAdminEmail,
        }, "Admin123!");

        var logger = new Mock<ILogger>();
        await UserSeedData.WarnIfDefaultSeedUsersExistInProductionAsync(
            userManager,
            CreateEnvironment(Environments.Production),
            logger.Object);

        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains(UserSeedData.DefaultAdminEmail, StringComparison.Ordinal)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task WarnIfDefaultSeedUsersExistInProductionAsync_InDevelopment_DoesNotLog()
    {
        await using var db = CreateDb();
        var userManager = CreateUserManager(db);
        await userManager.CreateAsync(new ApplicationUser
        {
            UserName = UserSeedData.DefaultAdminEmail,
            Email = UserSeedData.DefaultAdminEmail,
        }, "Admin123!");

        var logger = new Mock<ILogger>();
        await UserSeedData.WarnIfDefaultSeedUsersExistInProductionAsync(
            userManager,
            CreateEnvironment(Environments.Development),
            logger.Object);

        logger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task WarnIfDefaultSeedUsersExistInProductionAsync_WhenNoDefaultUsers_DoesNotLog()
    {
        await using var db = CreateDb();
        var userManager = CreateUserManager(db);
        var logger = new Mock<ILogger>();

        await UserSeedData.WarnIfDefaultSeedUsersExistInProductionAsync(
            userManager,
            CreateEnvironment(Environments.Production),
            logger.Object);

        logger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
}
