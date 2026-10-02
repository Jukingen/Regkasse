using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Limits;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class OfflineHealthControllerTests
{
    private static readonly Guid TenantId = SystemTenantIds.Platform;

    [Fact]
    public async Task GetSyncHealth_ReturnsHealthyWhenPendingBelowThreshold()
    {
        var registerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var db = CreateContext(TenantId);
        TenantTestDoubles.EnsurePlatformTenant(db);
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = TenantId,
            Id = registerId,
            RegisterNumber = "HLTH-K01",
            Location = "Test",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = now,
            Status = RegisterStatus.Open,
            CreatedAt = now,
            IsActive = true,
        });
        db.OfflineOrders.Add(new OfflineOrder
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            CashRegisterId = registerId,
            OfflineOrderId = "OFFLINE-20260714120000-0001",
            OrderData = "{}",
            OrderTotal = 10m,
            PaymentMethod = "cash",
            Status = OfflineOrderStatuses.Pending,
            SyncAttempts = 0,
            CreatedAtUtc = now.AddHours(-1),
            ExpiresAtUtc = now.AddHours(71),
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, new OfflineAlertRules { MaxPendingOrders = 50 });

        var result = await controller.GetSyncHealth(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var data = GetResponseData(ok.Value);
        Assert.NotNull(data);
        Assert.Equal(1, data!.PendingOrders);
        Assert.Equal(50, data.MaxPending);
        Assert.True(data.IsHealthy);
        Assert.Equal("healthy", data.Status);
    }

    [Fact]
    public async Task GetSyncHealth_ReturnsWarningWhenPendingAtThreshold()
    {
        var registerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var db = CreateContext(TenantId);
        TenantTestDoubles.EnsurePlatformTenant(db);
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = TenantId,
            Id = registerId,
            RegisterNumber = "HLTH-K02",
            Location = "Test",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = now,
            Status = RegisterStatus.Open,
            CreatedAt = now,
            IsActive = true,
        });

        for (var i = 0; i < 40; i++)
        {
            db.OfflineOrders.Add(new OfflineOrder
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                CashRegisterId = registerId,
                OfflineOrderId = $"OFFLINE-20260714120000-{i:0000}",
                OrderData = "{}",
                OrderTotal = 1m,
                PaymentMethod = "cash",
                Status = OfflineOrderStatuses.Pending,
                SyncAttempts = 0,
                CreatedAtUtc = now.AddMinutes(-i),
                ExpiresAtUtc = now.AddHours(71),
            });
        }

        await db.SaveChangesAsync();

        var controller = CreateController(db, new OfflineAlertRules { MaxPendingOrders = 50 });

        var result = await controller.GetSyncHealth(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var data = GetResponseData(ok.Value);
        Assert.NotNull(data);
        Assert.Equal(40, data!.PendingOrders);
        Assert.False(data.IsHealthy);
        Assert.Equal("warning", data.Status);
    }

    private static PosOfflineSyncHealthDto? GetResponseData(object? payload)
    {
        var dataProp = payload?.GetType().GetProperty("data");
        return dataProp?.GetValue(payload) as PosOfflineSyncHealthDto;
    }

    [Fact]
    public async Task GetSyncHealth_IncludesTenantOfflineTransactionCap()
    {
        await using var db = CreateContext(TenantId);
        TenantTestDoubles.EnsurePlatformTenant(db);
        await db.SaveChangesAsync();

        var caps = TenantLimits.CreateDefault(TenantId);
        var usage = new TenantLimitUsageDto
        {
            TenantId = TenantId,
            Limits = TenantLimitsDto.FromEntity(caps),
            CurrentOfflineTransactions = 40,
        };
        var controller = CreateController(db, new OfflineAlertRules { MaxPendingOrders = 50 }, usage);

        var result = await controller.GetSyncHealth(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var data = GetResponseData(ok.Value);
        Assert.NotNull(data);
        Assert.Equal(40, data!.CurrentOfflineTransactions);
        Assert.Equal(50, data.MaxOfflineTransactions);
    }

    [Fact]
    public async Task GetOfflineLimit_CountsPendingIntentsPlusPendingOrders()
    {
        var registerId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using var db = CreateContext(TenantId);
        TenantTestDoubles.EnsurePlatformTenant(db);
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = TenantId,
            Id = registerId,
            RegisterNumber = "LIM-K01",
            Location = "Test",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = now,
            Status = RegisterStatus.Open,
            CreatedAt = now,
            IsActive = true,
        });
        db.OfflineOrders.Add(new OfflineOrder
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            CashRegisterId = registerId,
            OfflineOrderId = "OFFLINE-LIMIT-0001",
            OrderData = "{}",
            OrderTotal = 10m,
            PaymentMethod = "cash",
            Status = OfflineOrderStatuses.Pending,
            SyncAttempts = 0,
            CreatedAtUtc = now.AddHours(-1),
            ExpiresAtUtc = now.AddHours(71),
        });
        db.OfflineOrders.Add(new OfflineOrder
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            CashRegisterId = registerId,
            OfflineOrderId = "OFFLINE-LIMIT-SYNCED",
            OrderData = "{}",
            OrderTotal = 10m,
            PaymentMethod = "cash",
            Status = OfflineOrderStatuses.Synced,
            SyncAttempts = 1,
            CreatedAtUtc = now.AddHours(-2),
            ExpiresAtUtc = now.AddHours(70),
        });
        await db.SaveChangesAsync();

        var caps = TenantLimits.CreateDefault(TenantId);
        var usage = new TenantLimitUsageDto
        {
            TenantId = TenantId,
            Limits = TenantLimitsDto.FromEntity(caps),
            CurrentOfflineTransactions = 2,
        };
        var controller = CreateController(db, new OfflineAlertRules { MaxPendingOrders = 50 }, usage);

        var result = await controller.GetOfflineLimit(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var data = GetLimitResponseData(ok.Value);
        Assert.NotNull(data);
        Assert.Equal(3, data!.CurrentOfflineTransactions);
        Assert.Equal(50, data.MaxOfflineTransactions);
        Assert.False(data.ApproachingLimit);
        Assert.False(data.LimitReached);
    }

    [Fact]
    public async Task GetOfflineLimit_SetsApproachingAndReachedFlags()
    {
        await using var db = CreateContext(TenantId);
        TenantTestDoubles.EnsurePlatformTenant(db);
        await db.SaveChangesAsync();

        var caps = TenantLimits.CreateDefault(TenantId);
        caps.MaxOfflineTransactions = 50;
        var approachingUsage = new TenantLimitUsageDto
        {
            TenantId = TenantId,
            Limits = TenantLimitsDto.FromEntity(caps),
            CurrentOfflineTransactions = 40,
        };
        var approaching = CreateController(
            db,
            new OfflineAlertRules { MaxPendingOrders = 50 },
            approachingUsage);
        var approachingResult = await approaching.GetOfflineLimit(CancellationToken.None);
        var approachingData = GetLimitResponseData(Assert.IsType<OkObjectResult>(approachingResult).Value);
        Assert.NotNull(approachingData);
        Assert.True(approachingData!.ApproachingLimit);
        Assert.False(approachingData.LimitReached);

        var reachedUsage = new TenantLimitUsageDto
        {
            TenantId = TenantId,
            Limits = TenantLimitsDto.FromEntity(caps),
            CurrentOfflineTransactions = 50,
        };
        var reached = CreateController(db, new OfflineAlertRules { MaxPendingOrders = 50 }, reachedUsage);
        var reachedResult = await reached.GetOfflineLimit(CancellationToken.None);
        var reachedData = GetLimitResponseData(Assert.IsType<OkObjectResult>(reachedResult).Value);
        Assert.NotNull(reachedData);
        Assert.True(reachedData!.ApproachingLimit);
        Assert.True(reachedData.LimitReached);
    }

    private static PosOfflineLimitDto? GetLimitResponseData(object? payload)
    {
        var dataProp = payload?.GetType().GetProperty("data");
        return dataProp?.GetValue(payload) as PosOfflineLimitDto;
    }

    private static OfflineHealthController CreateController(
        AppDbContext db,
        OfflineAlertRules alertRules,
        TenantLimitUsageDto? usage = null)
    {
        var alertRulesMonitor = new Mock<IOptionsMonitor<OfflineAlertRules>>();
        alertRulesMonitor.Setup(o => o.CurrentValue).Returns(alertRules);
        var guard = new Mock<ITenantLimitGuard>();
        var snapshot = usage ?? new TenantLimitUsageDto
        {
            TenantId = TenantId,
            Limits = TenantLimitsDto.FromEntity(TenantLimits.CreateDefault(TenantId)),
            CurrentOfflineTransactions = 0,
        };
        guard.Setup(g => g.GetUsageAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(snapshot);
        var accessor = TenantTestDoubles.TenantAccessorReturning(TenantId);
        return new OfflineHealthController(
            db,
            alertRulesMonitor.Object,
            guard.Object,
            accessor,
            NullLogger<OfflineHealthController>.Instance);
    }

    private static AppDbContext CreateContext(Guid tenantId)
    {
        var accessor = new CurrentTenantAccessor { TenantId = tenantId };
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"OfflineHealth_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, accessor);
    }
}
