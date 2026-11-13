using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Push;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DepExportPushNotificationServiceTests
{
    private static readonly Guid TenantId = SystemTenantIds.Platform;

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DepExportPush_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, new FixedTenantAccessor(TenantId));
    }

    private sealed class FixedTenantAccessor(Guid tenantId) : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; set; } = tenantId;
        public string? TenantSlug { get; set; }
    }

    private sealed class FakeTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
    }

    private static async Task SeedManagerAsync(AppDbContext db, string userId = "mgr-1")
    {
        TenantTestDoubles.EnsurePlatformTenant(db);
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = "manager1",
            NormalizedUserName = "MANAGER1",
            Email = "manager@example.com",
            NormalizedEmail = "MANAGER@EXAMPLE.COM",
            FirstName = "Max",
            LastName = "Manager",
            Role = Roles.Manager,
            IsActive = true,
        });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = userId,
            TenantId = TenantId,
            IsActive = true,
            IsOwner = true,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public void IsMilestoneEnabled_RespectsToggles()
    {
        var settings = new DepExportMobilePushSettings
        {
            PushEnabled = true,
            ThirtyDayReminder = true,
            SevenDayReminder = false,
            OneDayReminder = true,
            OverdueAlert = false,
        };

        Assert.True(settings.IsMilestoneEnabled(DepExportReminderMilestones.Days30));
        Assert.False(settings.IsMilestoneEnabled(DepExportReminderMilestones.Days7));
        Assert.True(settings.IsMilestoneEnabled(DepExportReminderMilestones.Days1));
        Assert.False(settings.IsMilestoneEnabled(DepExportReminderMilestones.Overdue));

        settings.PushEnabled = false;
        Assert.False(settings.IsMilestoneEnabled(DepExportReminderMilestones.Days30));
    }

    [Fact]
    public async Task SendReminderAsync_SendsToManagersWhenEnabled()
    {
        await using var db = CreateDb();
        await SeedManagerAsync(db);

        var sent = new List<PushNotification>();
        var push = new Mock<IPushNotificationService>();
        push.Setup(p => p.SendAsync(It.IsAny<PushNotification>(), It.IsAny<CancellationToken>()))
            .Callback<PushNotification, CancellationToken>((n, _) => sent.Add(n))
            .ReturnsAsync(true);

        var config = new Mock<INotificationConfigService>();
        config.Setup(c => c.GetAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NotificationConfig.CreateDefault());

        var sut = new DepExportPushNotificationService(
            db,
            push.Object,
            config.Object,
            new FakeTimeProvider(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            NullLogger<DepExportPushNotificationService>.Instance);

        var requirement = new DepExportRequirement
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            Title = "Jährlicher DEP Export",
            DueDate = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
        };

        await sut.SendReminderAsync(TenantId, requirement, DepExportReminderMilestones.Days30, 30);

        Assert.Single(sent);
        Assert.Equal("mgr-1", sent[0].UserId);
        Assert.Contains("30 Tagen", sent[0].Body);
        Assert.Equal("DepExportReminder", sent[0].Data!["Type"]);
    }

    [Fact]
    public async Task SendReminderAsync_SkipsWhenMilestoneDisabled()
    {
        await using var db = CreateDb();
        await SeedManagerAsync(db);

        var push = new Mock<IPushNotificationService>();
        var config = new Mock<INotificationConfigService>();
        config.Setup(c => c.GetAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationConfig
            {
                DepExportMobilePush = new DepExportMobilePushSettings
                {
                    PushEnabled = true,
                    ThirtyDayReminder = false,
                },
            });

        var sut = new DepExportPushNotificationService(
            db,
            push.Object,
            config.Object,
            TimeProvider.System,
            NullLogger<DepExportPushNotificationService>.Instance);

        await sut.SendReminderAsync(
            TenantId,
            new DepExportRequirement { Title = "x", DueDate = DateTime.UtcNow.AddDays(30) },
            DepExportReminderMilestones.Days30,
            30);

        push.Verify(
            p => p.SendAsync(It.IsAny<PushNotification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SaveSettingsAsync_PersistsViaNotificationConfig()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);
        await db.SaveChangesAsync();

        var notificationConfig = new NotificationConfigService(db);
        var sut = new DepExportPushNotificationService(
            db,
            Mock.Of<IPushNotificationService>(),
            notificationConfig,
            TimeProvider.System,
            NullLogger<DepExportPushNotificationService>.Instance);

        var saved = await sut.SaveSettingsAsync(
            TenantId,
            new DepExportMobilePushSettings
            {
                PushEnabled = true,
                ThirtyDayReminder = false,
                SevenDayReminder = true,
                OneDayReminder = false,
                OverdueAlert = true,
                SuccessNotification = false,
            });

        Assert.False(saved.ThirtyDayReminder);
        Assert.False(saved.OneDayReminder);
        Assert.False(saved.SuccessNotification);

        var reloaded = await sut.GetSettingsAsync(TenantId);
        Assert.False(reloaded.ThirtyDayReminder);
        Assert.True(reloaded.OverdueAlert);
    }
}
