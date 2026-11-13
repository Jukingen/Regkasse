using KasseAPI_Final.Services.License;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class GracePeriodReminderEmailComposerTests
{
    [Fact]
    public void BuildSubject_UsesUrgentWordingWhenTwoOrFewerDaysRemain()
    {
        var model = GracePeriodReminderEmailComposer.CreateModel(
            "Cafe Test",
            2,
            DateTime.UtcNow.Date.AddDays(2));
        var subject = GracePeriodReminderEmailComposer.BuildSubject(model);

        Assert.Contains("[DRINGEND]", subject, StringComparison.Ordinal);
        Assert.Contains("Grace-Period", subject, StringComparison.Ordinal);
        Assert.Contains("2", subject, StringComparison.Ordinal);
        Assert.Contains("Cafe Test", subject, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSubject_UsesErinnerungWhenMoreThanTwoDaysRemain()
    {
        var model = GracePeriodReminderEmailComposer.CreateModel(
            "Cafe Test",
            4,
            DateTime.UtcNow.Date.AddDays(4));
        var subject = GracePeriodReminderEmailComposer.BuildSubject(model);

        Assert.Contains("[Erinnerung]", subject, StringComparison.Ordinal);
        Assert.Contains("4", subject, StringComparison.Ordinal);
        Assert.DoesNotContain("[DRINGEND]", subject, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildHtmlBody_ContainsLockdownTimelineAndRenewalCta()
    {
        var lockdown = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var model = GracePeriodReminderEmailComposer.CreateModel(
            "Cafe Muster",
            6,
            lockdown,
            adminName: "Anna Admin",
            renewalLink: "https://admin.regkasse.at/license",
            supportEmail: "support@regkasse.at");
        var html = GracePeriodReminderEmailComposer.BuildHtmlBody(model);

        Assert.Contains("Cafe Muster", html, StringComparison.Ordinal);
        Assert.Contains("Anna Admin", html, StringComparison.Ordinal);
        Assert.Contains("01.08.2026", html, StringComparison.Ordinal);
        Assert.Contains("https://admin.regkasse.at/license", html, StringComparison.Ordinal);
        Assert.Contains("mailto:support@regkasse.at", html, StringComparison.Ordinal);
        Assert.Contains("Jetzt Lizenz verlängern", html, StringComparison.Ordinal);
        Assert.Contains("Was passiert nach der Grace-Period?", html, StringComparison.Ordinal);
        Assert.Contains("#faad14", html, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildPlainBody_IncludesSupportAndLockdownDate()
    {
        var model = GracePeriodReminderEmailComposer.CreateModel(
            "Cafe Muster",
            1,
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            supportEmail: "help@example.com");
        var plain = GracePeriodReminderEmailComposer.BuildPlainBody(model);

        Assert.Contains("DRINGEND", plain, StringComparison.Ordinal);
        Assert.Contains("Grace-Period", plain, StringComparison.Ordinal);
        Assert.Contains("01.08.2026", plain, StringComparison.Ordinal);
        Assert.Contains("help@example.com", plain, StringComparison.Ordinal);
    }
}
