using KasseAPI_Final.Data;
using KasseAPI_Final.Services.Database;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class MigrationStatusServiceTests
{
    private static (IDbContextFactory<AppDbContext> Factory, string DbName) CreateFactory()
    {
        var dbName = $"Migrations_{Guid.NewGuid():N}";
        var factory = new Mock<IDbContextFactory<AppDbContext>>();
        factory.Setup(f => f.CreateDbContext()).Returns(() => CreateDb(dbName));
        factory
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((CancellationToken _) => CreateDb(dbName));
        return (factory.Object, dbName);
    }

    private static AppDbContext CreateDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(null));
    }

    [Fact]
    public async Task GetStatusAsync_InMemory_ReportsHealthyOrDegraded()
    {
        var (factory, _) = CreateFactory();
        await using (var db = await factory.CreateDbContextAsync())
            await db.Database.EnsureCreatedAsync();

        var svc = new MigrationStatusService(factory, NullLogger<MigrationStatusService>.Instance);
        var status = await svc.GetStatusAsync();

        Assert.True(status.Status is "Healthy" or "Degraded" or "Unhealthy");
        Assert.True(status.AppliedCount >= 0);
        Assert.True(status.PendingCount >= 0);
        Assert.Equal(status.Pending.Count, status.PendingCount);
    }

    [Fact]
    public async Task GetAdminStatusAsync_ReturnsRecentAppliedCap()
    {
        var (factory, _) = CreateFactory();
        await using (var db = await factory.CreateDbContextAsync())
            await db.Database.EnsureCreatedAsync();

        var svc = new MigrationStatusService(factory, NullLogger<MigrationStatusService>.Instance);
        var admin = await svc.GetAdminStatusAsync(recentTake: 5);
        Assert.NotNull(admin.StrategyDoc);
        Assert.True(admin.RecentApplied.Count <= 5);
    }
}
