using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class UserUsernameHistoryServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"UsernameHist_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    [Fact]
    public async Task RecordChangeAsync_Persists_Row()
    {
        await using var db = CreateDb();
        db.Users.Add(new ApplicationUser
        {
            Id = "user-1",
            UserName = "newname",
            Email = "u@test.com",
            Role = Roles.Cashier,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var service = new UserUsernameHistoryService(db);
        await service.RecordChangeAsync("user-1", "oldname", "newname", "admin-1", "Support");

        var row = await db.UserUsernameHistories.AsNoTracking().SingleAsync();
        Assert.Equal("oldname", row.OldUsername);
        Assert.Equal("newname", row.NewUsername);
        Assert.Equal("admin-1", row.ChangedByUserId);
        Assert.Equal("Support", row.Reason);
    }

    [Fact]
    public async Task GetKnownUsernamesForUserAsync_Includes_Current_And_History()
    {
        await using var db = CreateDb();
        db.Users.Add(new ApplicationUser
        {
            Id = "user-1",
            UserName = "cashier3",
            Email = "u@test.com",
            Role = Roles.Cashier,
            IsActive = true,
        });
        db.UserUsernameHistories.Add(new UserUsernameHistory
        {
            UserId = "user-1",
            OldUsername = "cashier1",
            NewUsername = "cashier2",
            ChangedAtUtc = DateTime.UtcNow.AddDays(-10),
        });
        db.UserUsernameHistories.Add(new UserUsernameHistory
        {
            UserId = "user-1",
            OldUsername = "cashier2",
            NewUsername = "cashier3",
            ChangedAtUtc = DateTime.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();

        var service = new UserUsernameHistoryService(db);
        var names = await service.GetKnownUsernamesForUserAsync("user-1", "cashier3");

        Assert.Equal(new[] { "cashier1", "cashier2", "cashier3" }, names);
    }
}
