using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class NotificationConfigEvaluatorTests
{
    [Fact]
    public void ShouldDeliverInApp_respects_enabled_events_and_threshold()
    {
        var config = new NotificationConfig
        {
            InAppEnabled = true,
            EnabledEvents = new Dictionary<ActivityEventType, bool>
            {
                [ActivityEventType.BackupFailed] = false,
            },
            SeverityThreshold = new Dictionary<ActivityEventType, string>
            {
                [ActivityEventType.UserCreated] = ActivitySeverityNames.Warning,
            },
        };

        Assert.False(NotificationConfigEvaluator.ShouldDeliverInApp(
            config, ActivityEventType.BackupFailed, ActivitySeverityNames.Critical));
        Assert.False(NotificationConfigEvaluator.ShouldDeliverInApp(
            config, ActivityEventType.UserCreated, ActivitySeverityNames.Info));
        Assert.True(NotificationConfigEvaluator.ShouldDeliverInApp(
            config, ActivityEventType.UserCreated, ActivitySeverityNames.Warning));
    }

    [Fact]
    public void ShouldDeliverEmail_critical_events_ignore_email_disabled_when_recipients_exist()
    {
        var config = new NotificationConfig
        {
            EmailEnabled = false,
            EmailRecipients = { "admin@example.com" },
        };

        Assert.True(NotificationConfigEvaluator.ShouldDeliverEmail(
            config, ActivityEventType.BackupFailed, ActivitySeverityNames.Critical));
        Assert.True(NotificationConfigEvaluator.ShouldDeliverEmail(
            config, ActivityEventType.LicenseExpired, ActivitySeverityNames.Critical));
        Assert.False(NotificationConfigEvaluator.ShouldDeliverEmail(
            config, ActivityEventType.UserCreated, ActivitySeverityNames.Info));
    }
}
