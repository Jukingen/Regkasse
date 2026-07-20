using KasseAPI_Final.Models;
using Xunit;

namespace KasseAPI_Final.Tests;

public class WorkingHoursSettingsTests
{
    [Fact]
    public void CreateDefault_UsesOneHourReminderAndOpenWeek()
    {
        var hours = WorkingHoursSettings.CreateDefault();

        Assert.Equal(1, hours.ReminderHoursBeforeClosing);
        Assert.Equal(30, hours.StopOnlineOrdersMinutesBeforeClose);
        Assert.False(hours.AutoClosePOSAtClosing);
        Assert.Equal(WorkingHoursSettings.DefaultClosedDayMessage, hours.ClosedDayMessage);
        Assert.Empty(hours.SpecialDays);
        Assert.False(hours.Monday!.IsClosed);
        Assert.Equal("09:00", hours.Monday.OpenTime);
        Assert.Equal("22:00", hours.Monday.CloseTime);
        Assert.Equal("22:00", hours.GetDay(DayOfWeek.Sunday).CloseTime);
    }

    [Fact]
    public void Normalize_ClampsReminderAndFixesInvalidTimes()
    {
        var hours = new WorkingHoursSettings
        {
            ReminderHoursBeforeClosing = 99,
            StopOnlineOrdersMinutesBeforeClose = 999,
            ClosedDayMessage = "   ",
            Monday = new WorkingHoursDay { OpenTime = "bad", CloseTime = "25:99", IsClosed = false },
        };

        hours.Normalize();

        Assert.Equal(WorkingHoursSettings.MaxReminderHoursBeforeClosing, hours.ReminderHoursBeforeClosing);
        Assert.Equal(
            WorkingHoursSettings.MaxStopOnlineOrdersMinutesBeforeClose,
            hours.StopOnlineOrdersMinutesBeforeClose);
        Assert.Equal(WorkingHoursSettings.DefaultClosedDayMessage, hours.ClosedDayMessage);
        Assert.Equal("09:00", hours.Monday.OpenTime);
        Assert.Equal("22:00", hours.Monday.CloseTime);
    }

    [Fact]
    public void WorkingHoursDto_RoundTrips_IncludingSpecialDaysAndSettings()
    {
        var dto = new WorkingHoursDto
        {
            ReminderHoursBeforeClosing = 2,
            StopOnlineOrdersMinutesBeforeClose = 45,
            AutoClosePOSAtClosing = true,
            ClosedDayMessage = "Feiertag — geschlossen",
            Friday = new WorkingHoursDay { OpenTime = "10:00", CloseTime = "23:30", IsClosed = false },
            Sunday = new WorkingHoursDay { IsClosed = true, OpenTime = "00:00", CloseTime = "00:00" },
            SpecialDays =
            [
                new WorkingHoursSpecialDay
                {
                    Date = "2026-12-24",
                    IsClosed = true,
                },
                new WorkingHoursSpecialDay
                {
                    Date = "2026-12-31",
                    IsClosed = false,
                    OpenTime = "10:00",
                    CloseTime = "18:00",
                },
            ],
        };

        var settings = dto.ToSettings();
        var again = WorkingHoursDto.From(settings);

        Assert.Equal(2, again.ReminderHoursBeforeClosing);
        Assert.Equal(45, again.StopOnlineOrdersMinutesBeforeClose);
        Assert.True(again.AutoClosePOSAtClosing);
        Assert.Equal("Feiertag — geschlossen", again.ClosedDayMessage);
        Assert.Equal("10:00", again.Friday.OpenTime);
        Assert.Equal("23:30", again.Friday.CloseTime);
        Assert.True(again.Sunday.IsClosed);
        Assert.Equal(2, again.SpecialDays.Count);
        Assert.Equal("2026-12-24", again.SpecialDays[0].Date);
        Assert.True(again.SpecialDays[0].IsClosed);
        Assert.Equal("2026-12-31", again.SpecialDays[1].Date);
        Assert.Equal("10:00", again.SpecialDays[1].OpenTime);
        Assert.Equal("18:00", again.SpecialDays[1].CloseTime);
    }

    [Fact]
    public void ResolveDayForDate_AppliesSpecialDayOverride()
    {
        var hours = WorkingHoursSettings.CreateDefault();
        hours.Monday!.OpenTime = "09:00";
        hours.Monday.CloseTime = "22:00";
        hours.SpecialDays =
        [
            new WorkingHoursSpecialDay { Date = "2026-07-20", IsClosed = true },
        ];
        hours.Normalize();

        // 2026-07-20 is a Monday
        var resolved = hours.ResolveDayForDate(new DateOnly(2026, 7, 20));
        Assert.True(resolved.IsClosed);

        var regular = hours.ResolveDayForDate(new DateOnly(2026, 7, 21));
        Assert.False(regular.IsClosed);
        Assert.Equal("09:00", regular.OpenTime);
    }

    [Fact]
    public void IsAcceptingOnlineOrders_StopsBeforeClosingCutoff()
    {
        var hours = WorkingHoursSettings.CreateDefault();
        hours.StopOnlineOrdersMinutesBeforeClose = 30;
        // Monday 09:00–22:00
        hours.Monday = new WorkingHoursDay { OpenTime = "09:00", CloseTime = "22:00", IsClosed = false };
        hours.Normalize();

        var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
        // Monday 2026-07-20 21:40 Vienna → within 30 min of 22:00 → reject
        var localNearClose = new DateTime(2026, 7, 20, 21, 40, 0, DateTimeKind.Unspecified);
        var utcNearClose = new DateTimeOffset(localNearClose, tz.GetUtcOffset(localNearClose));
        Assert.False(hours.IsAcceptingOnlineOrders(utcNearClose, "Europe/Vienna"));

        // Monday 2026-07-20 18:00 Vienna → accept
        var localOpen = new DateTime(2026, 7, 20, 18, 0, 0, DateTimeKind.Unspecified);
        var utcOpen = new DateTimeOffset(localOpen, tz.GetUtcOffset(localOpen));
        Assert.True(hours.IsAcceptingOnlineOrders(utcOpen, "Europe/Vienna"));
    }

    [Fact]
    public void IsAcceptingOnlineOrders_RejectsClosedSpecialDay()
    {
        var hours = WorkingHoursSettings.CreateDefault();
        hours.SpecialDays =
        [
            new WorkingHoursSpecialDay { Date = "2026-12-24", IsClosed = true },
        ];
        hours.Normalize();

        var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
        var local = new DateTime(2026, 12, 24, 12, 0, 0, DateTimeKind.Unspecified);
        var utc = new DateTimeOffset(local, tz.GetUtcOffset(local));
        Assert.False(hours.IsAcceptingOnlineOrders(utc, "Europe/Vienna"));
        Assert.True(hours.IsClosedOn(utc, "Europe/Vienna"));
    }

    [Fact]
    public void EvaluateWebsiteStatus_RejectsBeforeOpen()
    {
        var hours = WorkingHoursSettings.CreateDefault();
        hours.Monday = new WorkingHoursDay { OpenTime = "09:00", CloseTime = "22:00", IsClosed = false };
        hours.Normalize();

        var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
        // Monday 2026-07-20 07:00 Vienna — before open
        var local = new DateTime(2026, 7, 20, 7, 0, 0, DateTimeKind.Unspecified);
        var utc = new DateTimeOffset(local, tz.GetUtcOffset(local));
        var status = hours.EvaluateWebsiteStatus(utc, "Europe/Vienna");

        Assert.False(status.IsOpen);
        Assert.False(status.CanOrder);
        Assert.Equal("09:00", status.OpenTime);
        Assert.Equal("22:00", status.CloseTime);
        Assert.Contains("09:00", status.Message);
    }

    [Fact]
    public void EvaluateWebsiteStatus_StopWindow_KeepsIsOpenButBlocksOrders()
    {
        var hours = WorkingHoursSettings.CreateDefault();
        hours.StopOnlineOrdersMinutesBeforeClose = 30;
        hours.Monday = new WorkingHoursDay { OpenTime = "09:00", CloseTime = "22:00", IsClosed = false };
        hours.Normalize();

        var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
        var local = new DateTime(2026, 7, 20, 21, 45, 0, DateTimeKind.Unspecified);
        var utc = new DateTimeOffset(local, tz.GetUtcOffset(local));
        var status = hours.EvaluateWebsiteStatus(utc, "Europe/Vienna");

        Assert.True(status.IsOpen);
        Assert.False(status.CanOrder);
        Assert.False(status.IsSpecial);
        Assert.Contains("Schließung", status.Message);
    }

    [Fact]
    public void EvaluateWebsiteStatus_MarksSpecialClosedDay()
    {
        var hours = WorkingHoursSettings.CreateDefault();
        hours.ClosedDayMessage = "Heiligabend geschlossen";
        hours.SpecialDays =
        [
            new WorkingHoursSpecialDay { Date = "2026-12-24", IsClosed = true },
        ];
        hours.Normalize();

        var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
        var local = new DateTime(2026, 12, 24, 12, 0, 0, DateTimeKind.Unspecified);
        var utc = new DateTimeOffset(local, tz.GetUtcOffset(local));
        var status = hours.EvaluateWebsiteStatus(utc, "Europe/Vienna");
        var special = hours.EvaluateSpecialDay(utc, "Europe/Vienna");

        Assert.True(status.IsSpecial);
        Assert.False(status.IsOpen);
        Assert.False(status.CanOrder);
        Assert.Equal("Heiligabend geschlossen", status.Message);

        Assert.True(special.IsSpecial);
        Assert.True(special.IsClosed);
        Assert.Equal("2026-12-24", special.Date);
    }

    [Fact]
    public void EvaluateSpecialDay_CustomHours_IsSpecialNotClosed()
    {
        var hours = WorkingHoursSettings.CreateDefault();
        hours.SpecialDays =
        [
            new WorkingHoursSpecialDay
            {
                Date = "2026-12-31",
                IsClosed = false,
                OpenTime = "10:00",
                CloseTime = "18:00",
            },
        ];
        hours.Normalize();

        var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
        var local = new DateTime(2026, 12, 31, 12, 0, 0, DateTimeKind.Unspecified);
        var utc = new DateTimeOffset(local, tz.GetUtcOffset(local));
        var special = hours.EvaluateSpecialDay(utc, "Europe/Vienna");
        var status = hours.EvaluateWebsiteStatus(utc, "Europe/Vienna");

        Assert.True(special.IsSpecial);
        Assert.False(special.IsClosed);
        Assert.Equal("10:00", special.OpenTime);
        Assert.Equal("18:00", special.CloseTime);
        Assert.True(status.IsSpecial);
        Assert.True(status.IsOpen);
        Assert.True(status.CanOrder);
    }
}
