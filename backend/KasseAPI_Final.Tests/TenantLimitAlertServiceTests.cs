using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Limits;
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

        var guard = new Mock<ITenantLimitGuard>();
        guard.Setup(g => g.GetUsageAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(usage);
        var activity = new Mock<IActivityEventPublisher>();
        var sut = new TenantLimitAlertService(guard.Object, activity.Object, NullLogger<TenantLimitAlertService>.Instance);

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
    public async Task PublishExceededAsync_UsesLimitExceededEvent()
    {
        var activity = new Mock<IActivityEventPublisher>();
        var sut = new TenantLimitAlertService(
            Mock.Of<ITenantLimitGuard>(),
            activity.Object,
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
}
