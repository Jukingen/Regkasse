using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ActivityEventServiceTests
{
    private static AppDbContext CreateDb(Guid tenantId)
    {
        var accessor = new FixedTenantAccessor(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options, accessor);
    }

    private static ActivityEventService CreateService(AppDbContext db) =>
        new(
            db,
            Mock.Of<IActivityEventEmailNotifier>(),
            Mock.Of<IActivityEventWebhookNotifier>(),
            new NotificationConfigService(db),
            Mock.Of<IActivityStreamHub>(),
            Options.Create(new ActivityNotificationOptions()),
            Mock.Of<ILogger<ActivityEventService>>());

    [Fact]
    public async Task PublishAsync_dedup_key_updates_existing_row()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var svc = CreateService(db);

        await svc.PublishAsync(
            new ActivityEventPublishRequest(tenantId, ActivityEventType.BackupFailed, "First", DedupKey: "bk"),
            CancellationToken.None);
        await svc.PublishAsync(
            new ActivityEventPublishRequest(tenantId, ActivityEventType.BackupFailed, "Second", DedupKey: "bk"),
            CancellationToken.None);

        var rows = await db.ActivityEvents.IgnoreQueryFilters().Where(e => e.TenantId == tenantId).ToListAsync();
        Assert.Single(rows);
        Assert.Equal("Second", rows[0].Title);
        Assert.Equal(ActivitySeverityNames.Critical, rows[0].Severity);
    }

    [Fact]
    public async Task MarkEventReadAsync_sets_per_user_read_state()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var svc = CreateService(db);
        const string userId = "user-1";

        var evt = await svc.PublishAsync(
            new ActivityEventPublishRequest(tenantId, ActivityEventType.UserCreated, "Created"),
            CancellationToken.None);

        var before = await svc.GetUnreadCountAsync(userId, tenantId, CancellationToken.None);
        Assert.Equal(1, before.UnreadCount);

        var dto = await svc.MarkEventReadAsync(userId, tenantId, evt.Id, CancellationToken.None);
        Assert.NotNull(dto);
        Assert.True(dto!.IsRead);
        Assert.NotNull(dto.ReadAtUtc);

        var after = await svc.GetUnreadCountAsync(userId, tenantId, CancellationToken.None);
        Assert.Equal(0, after.UnreadCount);
    }

    [Fact]
    public async Task ListAsync_filters_by_severity()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var svc = CreateService(db);
        const string userId = "user-1";

        await svc.PublishAsync(
            new ActivityEventPublishRequest(
                tenantId,
                ActivityEventType.UserCreated,
                "Info event",
                Severity: ActivitySeverityNames.Info),
            CancellationToken.None);
        await svc.PublishAsync(
            new ActivityEventPublishRequest(
                tenantId,
                ActivityEventType.BackupFailed,
                "Critical event",
                Severity: ActivitySeverityNames.Critical),
            CancellationToken.None);

        var filtered = await svc.ListAsync(userId, tenantId, 50, 0, ActivitySeverityNames.Critical, CancellationToken.None);
        Assert.Single(filtered.Items);
        Assert.Equal(ActivitySeverityNames.Critical, filtered.Items[0].Severity);
    }

    private sealed class FixedTenantAccessor(Guid tenantId) : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; set; } = tenantId;
    public string? TenantSlug { get; set; }
    }
}
