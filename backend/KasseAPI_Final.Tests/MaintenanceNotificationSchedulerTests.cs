using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Maintenance;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class MaintenanceNotificationSchedulerTests
{
    private sealed class FixedTenantAccessor(Guid? tenantId) : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; set; } = tenantId;
        public string? TenantSlug { get; set; }
    }

    private sealed class CapturingActivityPublisher : IActivityEventPublisher
    {
        public List<(Guid TenantId, ActivityEventType Type, string? DedupKey)> Events { get; } = new();

        public Task TryPublishAsync(
            Guid tenantId,
            ActivityEventType type,
            object? metadata = null,
            string? actorUserId = null,
            string? dedupKey = null,
            CancellationToken cancellationToken = default)
        {
            Events.Add((tenantId, type, dedupKey));
            return Task.CompletedTask;
        }
    }

    private static (MaintenanceNotificationScheduler Scheduler, AppDbContext Db, CapturingActivityPublisher Activity)
        CreateHarness()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"MaintSched_{Guid.NewGuid():N}")
            .Options;
        var db = new AppDbContext(options, new FixedTenantAccessor(null));
        var activity = new CapturingActivityPublisher();

        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<IActivityEventPublisher>(activity);
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var scheduler = new MaintenanceNotificationScheduler(
            scopeFactory,
            NullLogger<MaintenanceNotificationScheduler>.Instance);
        return (scheduler, db, activity);
    }

    private static async Task SeedTenantAsync(AppDbContext db)
    {
        db.Tenants.Add(new Tenant
        {
            Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            Name = "Dev",
            Slug = "dev",
            Status = TenantStatuses.Active,
            IsActive = true,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Process_24hWindow_EnablesForceDisplay_AndPublishesOnce()
    {
        var (scheduler, db, activity) = CreateHarness();
        await SeedTenantAsync(db);

        var now = DateTime.UtcNow;
        db.MaintenanceNotifications.Add(new MaintenanceNotification
        {
            Title = "Upgrade",
            Message = "API downtime",
            ScheduledStartAt = now.AddHours(18),
            ScheduledEndAt = now.AddHours(20),
            Status = MaintenanceNotificationStatuses.Published,
            PublishedAt = now.AddDays(-2),
            CreatedBy = "admin-1",
            AffectedSystems = MaintenanceAffectedSystems.All,
            IsForceDisplay = false,
        });
        await db.SaveChangesAsync();

        await scheduler.ProcessScheduledNotificationsAsync(CancellationToken.None);
        await scheduler.ProcessScheduledNotificationsAsync(CancellationToken.None);

        var row = await db.MaintenanceNotifications.SingleAsync();
        Assert.True(row.IsForceDisplay);
        Assert.NotNull(row.ForceDisplayFrom);
        Assert.NotNull(row.Reminder24hSentAt);
        Assert.Single(activity.Events, e => e.Type == ActivityEventType.MaintenanceForceDisplayEnabled);
    }

    [Fact]
    public async Task Process_PastStart_MarksInProgress()
    {
        var (scheduler, db, activity) = CreateHarness();
        await SeedTenantAsync(db);

        var now = DateTime.UtcNow;
        db.MaintenanceNotifications.Add(new MaintenanceNotification
        {
            Title = "Live",
            Message = "Window open",
            ScheduledStartAt = now.AddMinutes(-5),
            ScheduledEndAt = now.AddHours(2),
            Status = MaintenanceNotificationStatuses.Published,
            PublishedAt = now.AddDays(-1),
            CreatedBy = "admin-1",
            AffectedSystems = MaintenanceAffectedSystems.All,
        });
        await db.SaveChangesAsync();

        await scheduler.ProcessScheduledNotificationsAsync(CancellationToken.None);

        var row = await db.MaintenanceNotifications.SingleAsync();
        Assert.Equal(MaintenanceNotificationStatuses.InProgress, row.Status);
        Assert.Contains(activity.Events, e => e.Type == ActivityEventType.MaintenanceStarted);
    }

    [Fact]
    public async Task Process_7dWindow_SendsUpcomingReminder()
    {
        var (scheduler, db, activity) = CreateHarness();
        await SeedTenantAsync(db);

        var now = DateTime.UtcNow;
        db.MaintenanceNotifications.Add(new MaintenanceNotification
        {
            Title = "Week ahead",
            Message = "Plan ahead",
            ScheduledStartAt = now.AddDays(6.5),
            ScheduledEndAt = now.AddDays(6.5).AddHours(2),
            Status = MaintenanceNotificationStatuses.Published,
            PublishedAt = now,
            CreatedBy = "admin-1",
            AffectedSystems = MaintenanceAffectedSystems.All,
        });
        await db.SaveChangesAsync();

        await scheduler.ProcessScheduledNotificationsAsync(CancellationToken.None);

        var row = await db.MaintenanceNotifications.SingleAsync();
        Assert.NotNull(row.Reminder7dSentAt);
        Assert.Contains(activity.Events, e => e.Type == ActivityEventType.MaintenanceUpcoming);
    }
}
