using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DevCleanupControllerTests
{
    private static AppDbContext CreateDb() =>
        new(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"DevCleanup_{Guid.NewGuid():N}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options,
            NullCurrentTenantAccessor.Instance);

    private static DevCleanupController CreateController(
        AppDbContext db,
        bool isDevelopment)
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName)
            .Returns(isDevelopment ? Environments.Development : Environments.Production);

        return new DevCleanupController(
            env.Object,
            db,
            TenantTestDoubles.SettingsResolverReturning(LegacyDefaultTenantIds.Primary),
            Mock.Of<ILogger<DevCleanupController>>());
    }

    [Fact]
    public async Task CleanupOrphanedUsers_OutsideDevelopment_ReturnsNotFound()
    {
        await using var db = CreateDb();
        var controller = CreateController(db, isDevelopment: false);

        var result = await controller.CleanupOrphanedUsers();

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task CleanupOrphanedUsers_InDevelopment_RemovesMatchingUsersAndMemberships()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Test Bar",
            Slug = "prod",
            IsActive = false,
            Status = "deleted",
        });

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "admin@prod.regkasse.at",
            Email = "admin@prod.regkasse.at",
            NormalizedEmail = "ADMIN@BAR.REGKASSE.AT",
            NormalizedUserName = "ADMIN@BAR.REGKASSE.AT",
            FirstName = "Admin",
            LastName = "Bar",
            EmployeeNumber = "EMP-ORPH-1",
        };
        db.Users.Add(user);
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = tenantId,
            IsActive = true,
            IsOwner = true,
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, isDevelopment: true);
        var result = await controller.CleanupOrphanedUsers();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<DevOrphanedUserCleanupResponse>(ok.Value);
        Assert.Equal(1, body.DeletedUsers);
        Assert.Equal(1, body.DeletedMemberships);
        Assert.Empty(await db.Users.ToListAsync());
        Assert.Empty(await db.UserTenantMemberships.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task CleanupOrphanedUsers_InDevelopment_DoesNotRemoveUnrelatedUsers()
    {
        await using var db = CreateDb();
        db.Users.Add(new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "admin@dev.regkasse.at",
            Email = "admin@dev.regkasse.at",
            NormalizedEmail = "ADMIN@DEV.REGKASSE.AT",
            NormalizedUserName = "ADMIN@DEV.REGKASSE.AT",
            FirstName = "Admin",
            LastName = "Dev",
            EmployeeNumber = "EMP-KEEP-1",
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, isDevelopment: true);
        var result = await controller.CleanupOrphanedUsers();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<DevOrphanedUserCleanupResponse>(ok.Value);
        Assert.Equal(0, body.DeletedUsers);
        Assert.Single(await db.Users.ToListAsync());
    }
}
