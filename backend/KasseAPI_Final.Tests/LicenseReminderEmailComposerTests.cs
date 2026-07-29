using KasseAPI_Final.Services.License;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LicenseReminderEmailComposerTests
{
    [Fact]
    public void BuildSubject_UsesUrgentWordingWhenExpired()
    {
        var model = LicenseReminderEmailComposer.CreateModel("Cafe Test", 0, DateTime.UtcNow.Date);
        var subject = LicenseReminderEmailComposer.BuildSubject(model);

        Assert.Contains("[DRINGEND]", subject, StringComparison.Ordinal);
        Assert.Contains("abgelaufen", subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cafe Test", subject, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSubject_UsesErinnerungWithDayCount()
    {
        var model = LicenseReminderEmailComposer.CreateModel("Cafe Test", 14, DateTime.UtcNow.Date.AddDays(14));
        var subject = LicenseReminderEmailComposer.BuildSubject(model);

        Assert.Contains("[Erinnerung]", subject, StringComparison.Ordinal);
        Assert.Contains("14", subject, StringComparison.Ordinal);
        Assert.Contains("Cafe Test", subject, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, "#cf1322", "#cf1322")]
    [InlineData(7, "#faad14", "#d48806")]
    [InlineData(30, "#1890ff", "#1890ff")]
    public void ResolveUrgencyColors_MatchesBands(int days, string expectedBorder, string expectedAccent)
    {
        var (_, border, accent) = LicenseReminderEmailComposer.ResolveUrgencyColors(days);
        Assert.Equal(expectedBorder, border);
        Assert.Equal(expectedAccent, accent);
    }

    [Fact]
    public void BuildHtmlBody_ContainsTenantRenewalAndSupportMailto()
    {
        var model = LicenseReminderEmailComposer.CreateModel(
            "Cafe Muster",
            7,
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            adminName: "Anna Admin",
            renewalLink: "https://admin.regkasse.at/license",
            supportEmail: "support@regkasse.at");
        var html = LicenseReminderEmailComposer.BuildHtmlBody(model);

        Assert.Contains("Cafe Muster", html, StringComparison.Ordinal);
        Assert.Contains("Anna Admin", html, StringComparison.Ordinal);
        Assert.Contains("https://admin.regkasse.at/license", html, StringComparison.Ordinal);
        Assert.Contains("mailto:support@regkasse.at", html, StringComparison.Ordinal);
        Assert.Contains("Jetzt Lizenz verlängern", html, StringComparison.Ordinal);
        Assert.Contains("#fff7e6", html, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildPlainBody_IncludesSupportContact()
    {
        var model = LicenseReminderEmailComposer.CreateModel(
            "Cafe Muster",
            -2,
            DateTime.UtcNow.Date.AddDays(-2),
            supportEmail: "help@example.com");
        var plain = LicenseReminderEmailComposer.BuildPlainBody(model);

        Assert.Contains("abgelaufen", plain, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("help@example.com", plain, StringComparison.Ordinal);
    }
}
