using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.Reminder;
using KasseAPI_Final.Time;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TagesabschlussReminderWindowTests
{
    [Fact]
    public void IsInsideReminderWindow_true_at_vienna_22_local()
    {
        // 2026-07-16 20:00 UTC = 22:00 Vienna (CEST, UTC+2)
        var utc = new DateTime(2026, 7, 16, 20, 0, 0, DateTimeKind.Utc);
        var opt = new TagesabschlussReminderOptions { ReminderHourVienna = 22, WindowHours = 2 };

        Assert.True(TagesabschlussReminderWindow.IsInsideReminderWindow(utc, opt));
    }

    [Fact]
    public void IsInsideReminderWindow_false_before_vienna_22()
    {
        // 2026-07-16 19:30 UTC = 21:30 Vienna
        var utc = new DateTime(2026, 7, 16, 19, 30, 0, DateTimeKind.Utc);
        var opt = new TagesabschlussReminderOptions { ReminderHourVienna = 22, WindowHours = 2 };

        Assert.False(TagesabschlussReminderWindow.IsInsideReminderWindow(utc, opt));
    }

    [Fact]
    public void IsInsideReminderWindow_false_after_midnight_vienna()
    {
        // 2026-07-16 22:30 UTC = 00:30 next day Vienna
        var utc = new DateTime(2026, 7, 16, 22, 30, 0, DateTimeKind.Utc);
        var opt = new TagesabschlussReminderOptions { ReminderHourVienna = 22, WindowHours = 2 };

        Assert.False(TagesabschlussReminderWindow.IsInsideReminderWindow(utc, opt));
    }

    [Fact]
    public void BuildDedupKey_includes_register_and_vienna_day()
    {
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var day = new DateTime(2026, 7, 16, 0, 0, 0, DateTimeKind.Unspecified);
        Assert.Equal(
            "tagesabschluss_pending_aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee_2026-07-16",
            TagesabschlussReminderWindow.BuildDedupKey(id, day));
    }

    [Fact]
    public void AustriaTimeZone_is_available_for_window_math()
    {
        Assert.NotNull(PostgreSqlUtcDateTime.AustriaTimeZone);
    }
}
