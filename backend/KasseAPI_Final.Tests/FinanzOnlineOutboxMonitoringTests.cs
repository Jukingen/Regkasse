using System.Net;
using System.Reflection;
using System.Text;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Email;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FinanzOnlineOutboxMonitoringTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Summary_CountsPending_FailedLast24Hours_AndLastSuccess()
    {
        await using var db = CreateContext();
        var clock = new FixedTime(Now);
        db.FinanzOnlineOutboxMessages.AddRange(
            Row(TenantA, FinanzOnlineOutboxStatuses.Pending, Now.UtcDateTime.AddMinutes(-10)),
            Row(TenantA, FinanzOnlineOutboxStatuses.Pending, Now.UtcDateTime.AddHours(-2)),
            Row(TenantA, FinanzOnlineOutboxStatuses.RetryableFailure, Now.UtcDateTime.AddHours(-3)),
            Row(TenantA, FinanzOnlineOutboxStatuses.DeadLetter, Now.UtcDateTime.AddHours(-30)),
            Row(TenantA, FinanzOnlineOutboxStatuses.ProtocolSuccess, Now.UtcDateTime.AddHours(-1), processed: Now.UtcDateTime.AddMinutes(-20)),
            Row(TenantB, FinanzOnlineOutboxStatuses.Pending, Now.UtcDateTime));
        await db.SaveChangesAsync();

        var service = new FinanzOnlineOutboxMonitoringService(db, clock);
        var summary = await service.GetSummaryAsync(TenantA);

        Assert.Equal(2, summary.PendingCount);
        Assert.Equal(1, summary.FailedLast24Hours);
        Assert.Equal(Now.UtcDateTime.AddMinutes(-20), summary.LastSuccessfulSubmissionUtc);
    }

    [Fact]
    public async Task Summary_NullTenant_IncludesEveryMandant()
    {
        await using var db = CreateContext();
        db.FinanzOnlineOutboxMessages.AddRange(
            Row(TenantA, FinanzOnlineOutboxStatuses.Pending, Now.UtcDateTime),
            Row(TenantB, FinanzOnlineOutboxStatuses.Pending, Now.UtcDateTime));
        await db.SaveChangesAsync();

        var summary = await new FinanzOnlineOutboxMonitoringService(db, new FixedTime(Now)).GetSummaryAsync(null);

        Assert.Equal(2, summary.PendingCount);
    }

    [Fact]
    public async Task Alert_SendsWhenMoreThanFiveFailedInTheLastHour_AndDedupsTheHour()
    {
        await using var db = CreateContext();
        for (var i = 0; i < 6; i++)
            db.FinanzOnlineOutboxMessages.Add(Row(TenantA, FinanzOnlineOutboxStatuses.ProtocolFailure, Now.UtcDateTime.AddMinutes(-20)));
        db.FinanzOnlineOutboxMessages.Add(Row(TenantA, FinanzOnlineOutboxStatuses.PermanentFailure, Now.UtcDateTime.AddHours(-3)));
        await db.SaveChangesAsync();

        var alerts = new List<int>();
        var monitor = CreateMonitor(db, alerts);

        Assert.True(await monitor.CheckAndAlertAsync());
        Assert.Equal([6], alerts);
        Assert.False(await monitor.CheckAndAlertAsync());
        Assert.Equal([6], alerts);
    }

    [Fact]
    public async Task Alert_DoesNotSendAtThreshold()
    {
        await using var db = CreateContext();
        for (var i = 0; i < FinanzOnlineOutboxMonitoringService.HourlyFailureAlertThreshold; i++)
            db.FinanzOnlineOutboxMessages.Add(Row(TenantA, FinanzOnlineOutboxStatuses.RetryableFailure, Now.UtcDateTime.AddMinutes(-5)));
        await db.SaveChangesAsync();

        var alerts = new List<int>();
        Assert.False(await CreateMonitor(db, alerts).CheckAndAlertAsync());
        Assert.Empty(alerts);
    }

    [Fact]
    public void RetryAllFailed_RequiresSystemCritical()
    {
        var method = typeof(FinanzOnlineOutboxAdminController).GetMethod(nameof(FinanzOnlineOutboxAdminController.RetryAllFailed));
        var attribute = Assert.Single(method!.GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true).Cast<HasPermissionAttribute>());
        Assert.Equal(AppPermissions.SystemCritical, attribute.Permission);
    }

    [Fact]
    public async Task OpsAlert_PostsSlackText_AndSendsEmail()
    {
        string? posted = null;
        var handler = new CaptureHandler(body => posted = body);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));

        var email = new Mock<IEmailService>();
        email.SetupGet(e => e.IsConfigured).Returns(true);
        email.Setup(e => e.TrySendHtmlAndPlainAsync(
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var options = new Mock<IOptionsMonitor<ActivityNotificationOptions>>();
        options.SetupGet(o => o.CurrentValue).Returns(new ActivityNotificationOptions
        {
            WebhookEnabled = true,
            WebhookUrl = "https://hooks.example.test/slack",
            WebhookFormat = "Slack",
            FallbackEmailRecipients = "ops@regkasse.at",
        });

        var sink = new FinanzOnlineOutboxOpsAlert(
            email.Object,
            factory.Object,
            options.Object,
            NullLogger<FinanzOnlineOutboxOpsAlert>.Instance);

        Assert.True(await sink.SendAsync(6));
        Assert.Contains("\"text\":", posted, StringComparison.Ordinal);
        Assert.Contains("last hour: 6", posted, StringComparison.Ordinal);
        email.Verify(e => e.TrySendHtmlAndPlainAsync(
            It.Is<IReadOnlyCollection<string>>(list => list.Contains("ops@regkasse.at")),
            It.Is<string>(subject => subject.Contains("6", StringComparison.Ordinal)),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static FinanzOnlineOutboxFailureMonitor CreateMonitor(AppDbContext db, List<int> alerts)
    {
        var alert = new Mock<IFinanzOnlineOutboxOpsAlert>();
        alert.Setup(a => a.SendAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<int, CancellationToken>((count, _) => alerts.Add(count))
            .ReturnsAsync(true);

        var activity = new Mock<IActivityEventService>();
        activity.Setup(a => a.PublishAsync(It.IsAny<ActivityEventPublishRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ActivityEventPublishRequest, CancellationToken>((request, _) =>
            {
                db.ActivityEvents.Add(new ActivityEvent
                {
                    Id = Guid.NewGuid(),
                    TenantId = request.TenantId,
                    Type = request.Type,
                    Severity = ActivitySeverityNames.Error,
                    Title = request.Title,
                    Description = request.Description,
                    DedupKey = request.DedupKey,
                    CreatedAtUtc = Now.UtcDateTime,
                });
                db.SaveChanges();
            })
            .ReturnsAsync(new ActivityEvent());

        return new FinanzOnlineOutboxFailureMonitor(
            db,
            new FinanzOnlineOutboxMonitoringService(db, new FixedTime(Now)),
            alert.Object,
            activity.Object,
            new FixedTime(Now),
            NullLogger<FinanzOnlineOutboxFailureMonitor>.Instance);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("FonOutboxMonitor_" + Guid.NewGuid())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(TenantA));
    }

    private static FinanzOnlineOutboxMessage Row(Guid tenantId, string status, DateTime createdUtc, DateTime? processed = null) =>
        new()
        {
            TenantId = tenantId.ToString("D"),
            AggregateType = "Receipt",
            AggregateId = Guid.NewGuid(),
            MessageType = "SessionReceipt",
            BusinessKey = Guid.NewGuid().ToString("N"),
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            PayloadJson = "{}",
            PayloadHash = "hash",
            Status = status,
            CreatedAt = createdUtc,
            ProcessedAt = status == FinanzOnlineOutboxStatuses.Pending ? null : processed ?? createdUtc,
            NextAttemptAt = createdUtc,
            CorrelationId = "corr",
        };

    private sealed class FixedTime : TimeProvider
    {
        private readonly DateTimeOffset _utc;
        public FixedTime(DateTimeOffset utc) => _utc = utc;
        public override DateTimeOffset GetUtcNow() => _utc;
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        private readonly Action<string> _capture;
        public CaptureHandler(Action<string> capture) => _capture = capture;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            _capture(body);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok", Encoding.UTF8) };
        }
    }
}
