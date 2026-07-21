using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Offline;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class OfflineMonitoringServiceTests
{
    [Fact]
    public async Task GetOrderStatsAsync_CountsPendingAndExpired()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        var registerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var ctx = CreateContext(tenantId);
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = tenantId,
            Id = registerId,
            RegisterNumber = "MON-K01",
            Location = "Test",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = now,
            Status = RegisterStatus.Open,
            CreatedAt = now,
            IsActive = true,
        });
        ctx.OfflineOrders.AddRange(
            PendingOrder(tenantId, registerId, now.AddHours(-1), now.AddHours(71), 10m),
            PendingOrder(tenantId, registerId, now.AddHours(-2), now.AddHours(3), 5m));
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx, tenantId);

        var stats = await service.GetOrderStatsAsync();

        Assert.Equal(2, stats.Total);
        Assert.Equal(2, stats.Pending);
        Assert.Equal(0, stats.Synced);
        Assert.Equal(0, stats.Failed);
        Assert.Equal(0, stats.Expired);
    }

    [Fact]
    public async Task CheckAnomaliesAsync_FlagsTenantPendingLimitFromAlertRules()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        var registerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var ctx = CreateContext(tenantId);
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = tenantId,
            Id = registerId,
            RegisterNumber = "MON-K04",
            Location = "Test",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = now,
            Status = RegisterStatus.Open,
            CreatedAt = now,
            IsActive = true,
        });

        for (var i = 0; i < 3; i++)
        {
            ctx.OfflineOrders.Add(PendingOrder(tenantId, registerId, now.AddHours(-i), now.AddHours(70 - i), 1m));
        }

        await ctx.SaveChangesAsync();

        var service = CreateService(
            ctx,
            tenantId,
            alertRules: new OfflineAlertRules { MaxPendingOrders = 2 });

        var anomalies = await service.CheckAnomaliesAsync();

        Assert.Contains(anomalies, a =>
            a.Code == "too_many_pending"
            && a.Message.Contains("tenant limit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CheckAnomaliesAsync_FlagsBacklogAndExpiry()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        var registerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var ctx = CreateContext(tenantId);
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = tenantId,
            Id = registerId,
            RegisterNumber = "MON-K02",
            Location = "Test",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = now,
            Status = RegisterStatus.Open,
            CreatedAt = now,
            IsActive = true,
        });

        for (var i = 0; i < 11; i++)
        {
            ctx.OfflineOrders.Add(PendingOrder(tenantId, registerId, now.AddHours(-i), now.AddHours(70 - i), 1m));
        }

        await ctx.SaveChangesAsync();

        var service = CreateService(ctx, tenantId, new OfflineMonitoringOptions { OrderQueueAlertThreshold = 10 });

        var anomalies = await service.CheckAnomaliesAsync();

        Assert.Contains(anomalies, a => a.Code == "too_many_pending");
    }

    [Fact]
    public async Task GetSystemStatusAsync_ReturnsCriticalWhenExpiredPendingExists()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        var registerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var ctx = CreateContext(tenantId);
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = tenantId,
            Id = registerId,
            RegisterNumber = "MON-K03",
            Location = "Test",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = now,
            Status = RegisterStatus.Open,
            CreatedAt = now,
            IsActive = true,
        });
        ctx.OfflineOrders.Add(PendingOrder(tenantId, registerId, now.AddHours(-80), now.AddHours(-1), 9m));
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx, tenantId);

        var status = await service.GetSystemStatusAsync();

        Assert.True(status.HasCriticalIssues);
        Assert.Equal(1, status.TotalExpiredOrders);
    }

    private static OfflineOrder PendingOrder(
        Guid tenantId,
        Guid registerId,
        DateTime createdAt,
        DateTime expiresAt,
        decimal total) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CashRegisterId = registerId,
            OfflineOrderId = $"OFFLINE-{createdAt:yyyyMMddHHmmss}-0001",
            OrderData = "{}",
            OrderTotal = total,
            PaymentMethod = "cash",
            Status = OfflineOrderStatuses.Pending,
            SyncAttempts = 0,
            CreatedAtUtc = createdAt,
            ExpiresAtUtc = expiresAt,
        };

    private static AppDbContext CreateContext(Guid tenantId)
    {
        var accessor = new CurrentTenantAccessor { TenantId = tenantId };
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"OfflineMonitoring_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, accessor);
    }

    private static OfflineMonitoringService CreateService(
        AppDbContext ctx,
        Guid tenantId,
        OfflineMonitoringOptions? options = null,
        OfflineAlertRules? alertRules = null)
    {
        var accessor = new CurrentTenantAccessor { TenantId = tenantId };
        var optionsMonitor = new Mock<IOptionsMonitor<OfflineMonitoringOptions>>();
        optionsMonitor.Setup(o => o.CurrentValue).Returns(options ?? new OfflineMonitoringOptions());
        var alertRulesMonitor = new Mock<IOptionsMonitor<OfflineAlertRules>>();
        alertRulesMonitor.Setup(o => o.CurrentValue).Returns(alertRules ?? new OfflineAlertRules());
        var tseOptions = new Mock<IOptionsMonitor<TseOptions>>();
        tseOptions.Setup(o => o.CurrentValue).Returns(new TseOptions());
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        return new OfflineMonitoringService(
            ctx,
            accessor,
            optionsMonitor.Object,
            alertRulesMonitor.Object,
            tseOptions.Object,
            env.Object);
    }
}
