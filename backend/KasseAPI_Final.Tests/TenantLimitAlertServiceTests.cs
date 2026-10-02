using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Limits;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantLimitAlertServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task EvaluateAndPublishAsync_PublishesApproachingAndExceeded()
    {
        var caps = TenantLimits.CreateDefault(TenantId);
        caps.MaxProductsPerTenant = 10;
        caps.MaxUsersPerTenant = 10;
        var usage = new TenantLimitUsageDto
        {
            TenantId = TenantId,
            Limits = TenantLimitsDto.FromEntity(caps),
            CurrentProducts = 8,
            CurrentUsers = 10,
            CurrentDailyTransactions = 0,
            CurrentDailyRevenue = 0,
            CurrentBackups = 0,
            CurrentBackupSizeMb = 0,
            CurrentOfflineTransactions = 0,
            CurrentMaxAssignedRegistersPerUser = 0,
        };

        var activity = new Mock<IActivityEventPublisher>();
        await using var db = CreateContext();
        var sut = CreateSut(usage, activity, db);

        await sut.EvaluateAndPublishAsync(TenantId);

        activity.Verify(
            a => a.TryPublishAsync(
                It.Is<ActivityEventPublishRequest>(r =>
                    r.Type == ActivityEventType.LimitApproaching
                    && r.EntityId == TenantLimitKeys.MaxProductsPerTenant),
                It.IsAny<CancellationToken>()),
            Times.Once);
        activity.Verify(
            a => a.TryPublishAsync(
                It.Is<ActivityEventPublishRequest>(r =>
                    r.Type == ActivityEventType.LimitExceeded
                    && r.EntityId == TenantLimitKeys.MaxUsersPerTenant),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EvaluateAndPublishAsync_PublishesOfflineQueueApproachingLimit_At80Percent()
    {
        var activity = new Mock<IActivityEventPublisher>();
        await using var db = CreateContext();
        var sut = CreateSut(OfflineUsage(current: 40), activity, db);

        await sut.EvaluateAndPublishAsync(TenantId);

        activity.Verify(
            a => a.TryPublishAsync(
                It.Is<ActivityEventPublishRequest>(r =>
                    r.Type == ActivityEventType.OfflineQueueApproachingLimit
                    && r.EntityId == TenantLimitKeys.MaxOfflineTransactions
                    && r.TenantId == TenantId),
                It.IsAny<CancellationToken>()),
            Times.Once);
        activity.Verify(
            a => a.TryPublishAsync(
                It.Is<ActivityEventPublishRequest>(r => r.Type == ActivityEventType.LimitApproaching),
                It.IsAny<CancellationToken>()),
            Times.Never);
        activity.Verify(
            a => a.TryPublishAsync(
                It.Is<ActivityEventPublishRequest>(r => r.Type == ActivityEventType.LimitExceeded),
                It.IsAny<CancellationToken>()),
            Times.Never);

        var flag = Assert.Single(db.TenantSettings);
        Assert.Equal(TenantLimitAlertService.OfflineQueueApproachingLimitEmittedKey, flag.Key);
        Assert.Equal("true", flag.Value);
    }

    [Fact]
    public async Task EvaluateAndPublishAsync_DoesNotPublishOfflineQueueApproachingLimit_TwiceWhileStillAt80Percent()
    {
        var activity = new Mock<IActivityEventPublisher>();
        await using var db = CreateContext();
        var sut = CreateSut(OfflineUsage(current: 40), activity, db);

        await sut.EvaluateAndPublishAsync(TenantId);
        await sut.EvaluateAndPublishAsync(TenantId);

        activity.Verify(
            a => a.TryPublishAsync(
                It.Is<ActivityEventPublishRequest>(r =>
                    r.Type == ActivityEventType.OfflineQueueApproachingLimit),
                It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Single(db.TenantSettings);
    }

    [Fact]
    public async Task EvaluateAndPublishAsync_PublishesAgainAfterUsageDropsBelow80Percent()
    {
        var activity = new Mock<IActivityEventPublisher>();
        await using var db = CreateContext();
        var approaching = CreateSut(OfflineUsage(current: 40), activity, db);
        await approaching.EvaluateAndPublishAsync(TenantId);

        var recovered = CreateSut(OfflineUsage(current: 10), activity, db);
        await recovered.EvaluateAndPublishAsync(TenantId);

        var again = CreateSut(OfflineUsage(current: 41), activity, db);
        await again.EvaluateAndPublishAsync(TenantId);

        activity.Verify(
            a => a.TryPublishAsync(
                It.Is<ActivityEventPublishRequest>(r =>
                    r.Type == ActivityEventType.OfflineQueueApproachingLimit),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task PublishExceededAsync_UsesLimitExceededEvent()
    {
        var activity = new Mock<IActivityEventPublisher>();
        await using var db = CreateContext();
        var sut = new TenantLimitAlertService(
            Mock.Of<ITenantLimitGuard>(),
            activity.Object,
            db,
            NullLogger<TenantLimitAlertService>.Instance);

        await sut.PublishExceededAsync(
            TenantId,
            new LimitExceededException(TenantLimitKeys.MaxOfflineTransactions, 50, 50, "full"));

        activity.Verify(
            a => a.TryPublishAsync(
                It.Is<ActivityEventPublishRequest>(r =>
                    r.Type == ActivityEventType.LimitExceeded
                    && r.TenantId == TenantId
                    && r.EntityId == TenantLimitKeys.MaxOfflineTransactions),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static TenantLimitAlertService CreateSut(
        TenantLimitUsageDto usage,
        Mock<IActivityEventPublisher> activity,
        KasseAPI_Final.Data.AppDbContext db)
    {
        var guard = new Mock<ITenantLimitGuard>();
        guard.Setup(g => g.GetUsageAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(usage);
        return new TenantLimitAlertService(
            guard.Object,
            activity.Object,
            db,
            NullLogger<TenantLimitAlertService>.Instance);
    }

    private static TenantLimitUsageDto OfflineUsage(int current)
    {
        var caps = TenantLimits.CreateDefault(TenantId);
        caps.MaxOfflineTransactions = 50;
        return new TenantLimitUsageDto
        {
            TenantId = TenantId,
            Limits = TenantLimitsDto.FromEntity(caps),
            CurrentOfflineTransactions = current,
        };
    }

    private static KasseAPI_Final.Data.AppDbContext CreateContext()
    {
        var accessor = new CurrentTenantAccessor { TenantId = TenantId };
        var options = new DbContextOptionsBuilder<KasseAPI_Final.Data.AppDbContext>()
            .UseInMemoryDatabase($"TenantLimitAlert_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new KasseAPI_Final.Data.AppDbContext(options, accessor);
    }
}
