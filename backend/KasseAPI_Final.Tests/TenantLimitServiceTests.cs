using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Caching;
using KasseAPI_Final.Services.Metrics;
using KasseAPI_Final.Services.Limits;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantLimitServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TenantLimits_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static TenantLimitService CreateService(AppDbContext db)
    {
        var cache = new TenantLimitCacheService(
            new MemoryCacheService(
                new MemoryCache(new MemoryCacheOptions()),
                NullLogger<MemoryCacheService>.Instance,
                new CacheMetricsService()),
            Options.Create(new KasseAPI_Final.Configuration.CacheSettings()),
            NullLogger<TenantLimitCacheService>.Instance);
        return new TenantLimitService(db, cache, NullLogger<TenantLimitService>.Instance);
    }

    private static async Task SeedTenantAsync(AppDbContext db)
    {
        db.Tenants.Add(new Tenant
        {
            Id = TenantId,
            Name = "Limits tenant",
            Slug = "limits-tenant",
            IsActive = true,
            Status = TenantStatuses.Active,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetLimitsAsync_MissingRow_ReturnsDefaults()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db);
        var service = CreateService(db);

        var limits = await service.GetLimitsAsync(TenantId);

        Assert.Equal(TenantLimits.DefaultMaxActiveRegistersPerUser, limits.MaxActiveRegistersPerUser);
        Assert.Equal(TenantLimits.DefaultMaxOfflineTransactions, limits.MaxOfflineTransactions);
        Assert.Single(db.TenantLimits.IgnoreQueryFilters());
    }

    [Fact]
    public async Task GetLimitsAsync_UnknownTenant_Throws()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetLimitsAsync(Guid.NewGuid()));
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateThenGet_ReturnsPersistedValues()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db);
        var service = CreateService(db);

        await service.UpdateLimitsAsync(TenantId, new UpdateTenantLimitsRequest
        {
            MaxActiveRegistersPerUser = 2,
            MaxProductsPerTenant = 100,
            MaxUsersPerTenant = 8,
            DailyMaxTransactions = 20,
            MaxTransactionAmount = 250.50m,
            DailyMaxRevenue = 1000m,
            MaxBackupsPerTenant = 4,
            MaxBackupSizeMb = 80,
            MaxOfflineTransactions = 7,
        });

        var limits = await service.GetLimitsAsync(TenantId);
        Assert.Equal(2, limits.MaxActiveRegistersPerUser);
        Assert.Equal(250.50m, limits.MaxTransactionAmount);
        Assert.Equal(7, limits.MaxOfflineTransactions);
        Assert.Equal(2, await service.GetLimitValueAsync(TenantId, TenantLimitKeys.MaxActiveRegistersPerUser));
    }

    [Fact]
    public async Task CheckLimitAsync_IsTrueBelowCap_FalseAtCap()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db);
        var service = CreateService(db);
        await service.UpdateLimitsAsync(TenantId, new UpdateTenantLimitsRequest
        {
            MaxActiveRegistersPerUser = 2,
            MaxProductsPerTenant = TenantLimits.DefaultMaxProductsPerTenant,
            MaxUsersPerTenant = TenantLimits.DefaultMaxUsersPerTenant,
            DailyMaxTransactions = TenantLimits.DefaultDailyMaxTransactions,
            MaxTransactionAmount = TenantLimits.DefaultMaxTransactionAmount,
            DailyMaxRevenue = TenantLimits.DefaultDailyMaxRevenue,
            MaxBackupsPerTenant = TenantLimits.DefaultMaxBackupsPerTenant,
            MaxBackupSizeMb = TenantLimits.DefaultMaxBackupSizeMb,
            MaxOfflineTransactions = TenantLimits.DefaultMaxOfflineTransactions,
        });

        Assert.True(await service.CheckLimitAsync(TenantId, TenantLimitKeys.MaxActiveRegistersPerUser, 1));
        Assert.False(await service.CheckLimitAsync(TenantId, TenantLimitKeys.MaxActiveRegistersPerUser, 2));
    }

    [Fact]
    public async Task ResetLimitsAsync_RestoresDefaults()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db);
        var service = CreateService(db);
        await service.UpdateLimitsAsync(TenantId, new UpdateTenantLimitsRequest
        {
            MaxActiveRegistersPerUser = 1,
            MaxProductsPerTenant = 1,
            MaxUsersPerTenant = 1,
            DailyMaxTransactions = 1,
            MaxTransactionAmount = 1m,
            DailyMaxRevenue = 1m,
            MaxBackupsPerTenant = 1,
            MaxBackupSizeMb = 1,
            MaxOfflineTransactions = 1,
        });

        await service.ResetLimitsAsync(TenantId);

        var limits = await service.GetLimitsAsync(TenantId);
        Assert.Equal(TenantLimits.DefaultMaxActiveRegistersPerUser, limits.MaxActiveRegistersPerUser);
        Assert.Equal(TenantLimits.DefaultMaxOfflineTransactions, limits.MaxOfflineTransactions);
        Assert.Equal(TenantLimits.DefaultMaxTransactionAmount, limits.MaxTransactionAmount);
    }

    [Fact]
    public async Task UpdateLimitsAsync_AppliesOnlyProvidedFields()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db);
        var service = CreateService(db);

        await service.GetLimitsAsync(TenantId);
        await service.UpdateLimitsAsync(TenantId, new UpdateTenantLimitsRequest
        {
            MaxActiveRegistersPerUser = 3,
        });

        var limits = await service.GetLimitsAsync(TenantId);
        Assert.Equal(3, limits.MaxActiveRegistersPerUser);
        Assert.Equal(TenantLimits.DefaultMaxOfflineTransactions, limits.MaxOfflineTransactions);
    }

    [Fact]
    public async Task SetLimitValueAsync_UpdatesSingleKeyAndInvalidatesCache()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db);
        var service = CreateService(db);
        await service.GetLimitsAsync(TenantId);

        await service.SetLimitValueAsync(TenantId, TenantLimitKeys.MaxProductsPerTenant, 42);

        var limits = await service.GetLimitsAsync(TenantId);
        Assert.Equal(42, limits.MaxProductsPerTenant);
        Assert.Equal(TenantLimits.DefaultMaxUsersPerTenant, limits.MaxUsersPerTenant);
        Assert.Equal(42, await service.GetLimitValueAsync(TenantId, TenantLimitKeys.MaxProductsPerTenant));
    }

    [Fact]
    public async Task SetLimitValueAsync_UnknownKey_Throws()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db);
        var service = CreateService(db);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.SetLimitValueAsync(TenantId, "notARealLimit", 1));
    }
}
