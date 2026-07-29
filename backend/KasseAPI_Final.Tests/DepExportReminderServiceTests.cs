using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DepExportReminderMilestonesTests
{
    [Theory]
    [InlineData(30, DepExportReminderMilestones.Days30)]
    [InlineData(7, DepExportReminderMilestones.Days7)]
    [InlineData(1, DepExportReminderMilestones.Days1)]
    [InlineData(-1, DepExportReminderMilestones.Overdue)]
    [InlineData(-10, DepExportReminderMilestones.Overdue)]
    [InlineData(0, null)]
    [InlineData(2, null)]
    [InlineData(29, null)]
    [InlineData(31, null)]
    public void ResolveMilestone_MatchesProductWindows(int daysUntilDue, string? expected)
    {
        Assert.Equal(expected, DepExportReminderMilestones.ResolveMilestone(daysUntilDue));
    }

    [Fact]
    public void DaysUntilDue_UsesUtcCalendarDates()
    {
        var due = new DateTime(2026, 1, 31, 23, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc);
        Assert.Equal(30, DepExportReminderMilestones.DaysUntilDue(due, now));
    }

    [Fact]
    public void BuildDedupKey_OverdueIncludesCalendarDay()
    {
        var req = new DepExportRequirement
        {
            Category = DepExportRequirementCategories.Yearly,
            PeriodStart = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var key = DepExportReminderMilestones.BuildDedupKey(
            tenantId,
            req,
            DepExportReminderMilestones.Overdue,
            new DateTime(2026, 2, 5, 12, 0, 0, DateTimeKind.Utc));

        Assert.Contains(":overdue:2026-02-05", key);
        Assert.Contains("Yearly", key);
    }

    [Fact]
    public void EventTypeFor_MapsOverdueSeparately()
    {
        Assert.Equal(
            ActivityEventType.DepExportDueSoon,
            DepExportReminderMilestones.EventTypeFor(DepExportReminderMilestones.Days7));
        Assert.Equal(
            ActivityEventType.DepExportOverdue,
            DepExportReminderMilestones.EventTypeFor(DepExportReminderMilestones.Overdue));
    }
}

public sealed class DepExportReminderServiceTests
{
    private static readonly Guid TenantId = LegacyDefaultTenantIds.Primary;

    private sealed class FixedTenantAccessor : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; set; }
        public string? TenantSlug { get; set; }
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }

    private static (DepExportReminderService Sut, List<ActivityEventPublishRequest> Published, AppDbContext Db)
        CreateSut(
            DateTime utcNow,
            IReadOnlyList<DepExportRequirement> requirements,
            DepExportReminderOptions? options = null)
    {
        var tenantAccessor = new FixedTenantAccessor();
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DepExportReminder_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AppDbContext(dbOptions, tenantAccessor);
        TenantTestDoubles.EnsureDefaultTenant(db);
        db.SaveChanges();

        var requirementService = new Mock<IDepExportRequirementService>();
        requirementService
            .Setup(s => s.GetRequirementsAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(requirements);

        var published = new List<ActivityEventPublishRequest>();
        var activity = new Mock<IActivityEventService>();
        activity
            .Setup(a => a.PublishAsync(It.IsAny<ActivityEventPublishRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ActivityEventPublishRequest, CancellationToken>((req, _) =>
            {
                published.Add(req);
                db.ActivityEvents.Add(new ActivityEvent
                {
                    TenantId = req.TenantId,
                    Type = req.Type,
                    Title = req.Title,
                    Description = req.Description,
                    DedupKey = req.DedupKey,
                    Severity = req.Severity ?? "warning",
                    CreatedAtUtc = utcNow,
                });
                db.SaveChanges();
            })
            .ReturnsAsync(new ActivityEvent());

        var services = new ServiceCollection();
        services.AddSingleton<ICurrentTenantAccessor>(tenantAccessor);
        services.AddSingleton(db);
        services.AddSingleton(requirementService.Object);
        services.AddSingleton(activity.Object);
        services.AddSingleton(Mock.Of<IDepExportPushNotificationService>());
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var reminderOptions = options ?? new DepExportReminderOptions { Enabled = true };
        var monitor = new Mock<IOptionsMonitor<DepExportReminderOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(reminderOptions);

        var sut = new DepExportReminderService(
            scopeFactory,
            monitor.Object,
            new FixedTimeProvider(utcNow),
            NullLogger<DepExportReminderService>.Instance);

        return (sut, published, db);
    }

    private static DepExportRequirement YearlyLegal(DateTime due, bool completed = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            RequirementType = DepExportRequirementTypes.Legal,
            Category = DepExportRequirementCategories.Yearly,
            Title = "Jährlicher DEP Export",
            Description = "Export für das Jahr 2025",
            DueDate = due,
            IsCompleted = completed,
            Priority = 5,
            PeriodStart = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodEnd = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc),
        };

    [Fact]
    public async Task CheckAndNotifyAsync_Publishes30DayLegalReminder()
    {
        var due = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var (sut, published, db) = CreateSut(now, [YearlyLegal(due)]);

        await using (db)
        {
            var result = await sut.CheckAndNotifyAsync();

            Assert.Equal(1, result.RemindersSent);
            var evt = Assert.Single(published);
            Assert.Equal(ActivityEventType.DepExportDueSoon, evt.Type);
            Assert.Contains("30d", evt.DedupKey);
            Assert.Equal(ActivitySeverityNames.Warning, evt.Severity);
        }
    }

    [Fact]
    public async Task CheckAndNotifyAsync_PublishesOverdueReminder()
    {
        var due = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 2, 10, 12, 0, 0, DateTimeKind.Utc);
        var (sut, published, db) = CreateSut(now, [YearlyLegal(due)]);

        await using (db)
        {
            var result = await sut.CheckAndNotifyAsync();

            Assert.Equal(1, result.RemindersSent);
            var evt = Assert.Single(published);
            Assert.Equal(ActivityEventType.DepExportOverdue, evt.Type);
            Assert.Contains("overdue", evt.DedupKey);
        }
    }

    [Fact]
    public async Task CheckAndNotifyAsync_SkipsCompletedAndNonMilestoneDays()
    {
        var due = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc); // 16 days left
        var (sut, published, db) = CreateSut(
            now,
            [
                YearlyLegal(due, completed: true),
                YearlyLegal(due, completed: false),
            ]);

        await using (db)
        {
            var result = await sut.CheckAndNotifyAsync();
            Assert.Equal(0, result.RemindersSent);
            Assert.Empty(published);
        }
    }

    [Fact]
    public async Task CheckAndNotifyAsync_DedupesSameMilestone()
    {
        var due = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var (sut, published, db) = CreateSut(now, [YearlyLegal(due)]);

        await using (db)
        {
            var first = await sut.CheckAndNotifyAsync();
            var second = await sut.CheckAndNotifyAsync();

            Assert.Equal(1, first.RemindersSent);
            Assert.Equal(0, second.RemindersSent);
            Assert.Single(published);
        }
    }

    [Fact]
    public async Task CheckAndNotifyAsync_Disabled_ReturnsZeros()
    {
        var (sut, published, db) = CreateSut(
            DateTime.UtcNow,
            [YearlyLegal(DateTime.UtcNow.AddDays(30))],
            new DepExportReminderOptions { Enabled = false });

        await using (db)
        {
            var result = await sut.CheckAndNotifyAsync();
            Assert.Equal(0, result.TenantsScanned);
            Assert.Equal(0, result.RemindersSent);
            Assert.Empty(published);
        }
    }
}
