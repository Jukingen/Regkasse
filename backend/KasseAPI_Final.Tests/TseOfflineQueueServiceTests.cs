using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Offline;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseOfflineQueueServiceTests
{
    [Fact]
    public async Task GetQueueStatusAsync_FlagsWarningAndCriticalByTenantTotals()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        var registerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var ctx = CreateContext(tenantId);
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        ctx.CashRegisters.Add(Register(tenantId, registerId, "Q-K01", now));

        for (var i = 0; i < 30; i++)
        {
            ctx.OfflineTransactions.Add(NonFiscal(tenantId, registerId, now.AddMinutes(-i)));
        }

        await ctx.SaveChangesAsync();

        var activity = new Mock<IActivityEventPublisher>();
        var audit = new Mock<IAuditLogService>();
        var service = CreateService(ctx, activity.Object, audit.Object);

        var warningStatus = await service.GetQueueStatusAsync(tenantId);
        Assert.Equal(30, warningStatus.TotalQueued);
        Assert.True(warningStatus.IsWarning);
        Assert.False(warningStatus.IsCritical);
        Assert.Single(warningStatus.ByRegister);
        Assert.Equal(30, warningStatus.ByRegister[0].QueuedCount);

        for (var i = 0; i < 20; i++)
        {
            ctx.OfflineTransactions.Add(NonFiscal(tenantId, registerId, now.AddMinutes(-(30 + i))));
        }

        await ctx.SaveChangesAsync();

        var criticalStatus = await service.GetQueueStatusAsync(tenantId);
        Assert.Equal(50, criticalStatus.TotalQueued);
        Assert.True(criticalStatus.IsCritical);
        Assert.True(criticalStatus.ByRegister[0].IsAtCap);
    }

    [Fact]
    public async Task SoftClearQueueAsync_RequiresConfirmToken_AndMarksFailed()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        var registerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var ctx = CreateContext(tenantId);
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        ctx.CashRegisters.Add(Register(tenantId, registerId, "Q-K02", now));
        ctx.OfflineTransactions.Add(NonFiscal(tenantId, registerId, now));
        ctx.OfflineTransactions.Add(new OfflineTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CashRegisterId = registerId,
            PayloadJson = """{"totalAmount":1,"payment":{"method":"cash"}}""",
            ServerReceivedAtUtc = now,
            OfflineCreatedAtUtc = now,
            Status = OfflineTransactionStatus.Synced,
            CreatedAt = now,
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var activity = new Mock<IActivityEventPublisher>();
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

        var service = CreateService(ctx, activity.Object, audit.Object);

        var rejected = await service.SoftClearQueueAsync(tenantId, "CLEAR", "nope", "actor-1");
        Assert.False(rejected.Success);
        Assert.Equal(1, await ctx.OfflineTransactions.CountAsync(x => x.Status == OfflineTransactionStatus.NonFiscalPending));

        var cleared = await service.SoftClearQueueAsync(
            tenantId,
            TseOfflineQueueService.SoftClearConfirmToken,
            "ops cleanup",
            "actor-1");
        Assert.True(cleared.Success);
        Assert.Equal(1, cleared.SoftClearedCount);
        Assert.Equal(0, await ctx.OfflineTransactions.CountAsync(x => x.Status == OfflineTransactionStatus.NonFiscalPending));
        Assert.Equal(1, await ctx.OfflineTransactions.CountAsync(x => x.Status == OfflineTransactionStatus.Failed));
        Assert.Equal(1, await ctx.OfflineTransactions.CountAsync(x => x.Status == OfflineTransactionStatus.Synced));
    }

    [Fact]
    public async Task SendQueueAlertAsync_PublishesWhenAboveWarning()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        var registerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var ctx = CreateContext(tenantId);
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        ctx.CashRegisters.Add(Register(tenantId, registerId, "Q-K03", now));
        for (var i = 0; i < 31; i++)
            ctx.OfflineTransactions.Add(NonFiscal(tenantId, registerId, now.AddMinutes(-i)));
        await ctx.SaveChangesAsync();

        var activity = new Mock<IActivityEventPublisher>();
        activity.Setup(a => a.TryPublishAsync(
                tenantId,
                ActivityEventType.OfflineQueueGrowing,
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var audit = new Mock<IAuditLogService>();
        var service = CreateService(ctx, activity.Object, audit.Object);

        var result = await service.SendQueueAlertAsync(tenantId);
        Assert.True(result.Sent);
        Assert.Equal("Warning", result.Severity);

        activity.Verify(
            a => a.TryPublishAsync(
                tenantId,
                ActivityEventType.OfflineQueueGrowing,
                It.IsAny<object?>(),
                "system",
                It.Is<string?>(k => k != null && k.StartsWith("tse-offline-queue:", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static TseOfflineQueueService CreateService(
        AppDbContext ctx,
        IActivityEventPublisher activity,
        IAuditLogService audit)
    {
        var tse = new Mock<IOptionsMonitor<TseOptions>>();
        tse.Setup(o => o.CurrentValue).Returns(new TseOptions { MaxOfflineTransactionsPerCashRegister = 50 });
        var monitoring = new Mock<IOptionsMonitor<OfflineMonitoringOptions>>();
        monitoring.Setup(o => o.CurrentValue).Returns(new OfflineMonitoringOptions
        {
            TseOfflineQueueWarningThreshold = 30,
            TseOfflineQueueCriticalThreshold = 50,
            TseOfflineCapWarningPercent = 80,
        });

        return new TseOfflineQueueService(
            ctx,
            activity,
            audit,
            tse.Object,
            monitoring.Object,
            NullLogger<TseOfflineQueueService>.Instance);
    }

    private static AppDbContext CreateContext(Guid tenantId)
    {
        var accessor = new CurrentTenantAccessor { TenantId = tenantId };
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TseOfflineQueue_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, accessor);
    }

    private static CashRegister Register(Guid tenantId, Guid id, string number, DateTime now) =>
        new()
        {
            TenantId = tenantId,
            Id = id,
            RegisterNumber = number,
            Location = "Test",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = now,
            Status = RegisterStatus.Open,
            CreatedAt = now,
            IsActive = true,
        };

    private static OfflineTransaction NonFiscal(Guid tenantId, Guid registerId, DateTime receivedAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CashRegisterId = registerId,
            PayloadJson = """{"totalAmount":12.5,"payment":{"method":"cash"}}""",
            ServerReceivedAtUtc = receivedAt,
            OfflineCreatedAtUtc = receivedAt,
            Status = OfflineTransactionStatus.NonFiscalPending,
            CreatedAt = receivedAt,
            IsActive = true,
        };
}
