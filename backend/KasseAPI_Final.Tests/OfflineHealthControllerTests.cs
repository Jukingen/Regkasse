using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
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
    private static readonly Guid TenantId = LegacyDefaultTenantIds.Primary;

    [Fact]
    public async Task GetSyncHealth_ReturnsHealthyWhenPendingBelowThreshold()
    {
        var registerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var db = CreateContext(TenantId);
        TenantTestDoubles.EnsureDefaultTenant(db);
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
        TenantTestDoubles.EnsureDefaultTenant(db);
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

    private static OfflineHealthController CreateController(
        AppDbContext db,
        OfflineAlertRules alertRules)
    {
        var alertRulesMonitor = new Mock<IOptionsMonitor<OfflineAlertRules>>();
        alertRulesMonitor.Setup(o => o.CurrentValue).Returns(alertRules);
        return new OfflineHealthController(db, alertRulesMonitor.Object, NullLogger<OfflineHealthController>.Instance);
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
