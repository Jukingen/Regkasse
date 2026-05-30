using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupScheduleCronCodecTests
{
    [Fact]
    public void BuildCron_daily()
    {
        var cron = BackupScheduleCronCodec.BuildCron(new BackupScheduleConfigurationDto
        {
            Frequency = BackupScheduleFrequency.Daily,
            HourUtc = 3,
            MinuteUtc = 15,
        });
        Assert.Equal("15 3 * * *", cron);
    }

    [Fact]
    public void BuildCron_weekly_monday()
    {
        var cron = BackupScheduleCronCodec.BuildCron(new BackupScheduleConfigurationDto
        {
            Frequency = BackupScheduleFrequency.Weekly,
            HourUtc = 2,
            MinuteUtc = 0,
            DayOfWeek = 1,
        });
        Assert.Equal("0 2 * * 1", cron);
    }

    [Fact]
    public void TryParseCron_roundtrip_monthly()
    {
        const string cron = "30 4 15 * *";
        Assert.True(BackupScheduleCronCodec.TryParseCron(cron, out var cfg));
        Assert.NotNull(cfg);
        Assert.Equal(BackupScheduleFrequency.Monthly, cfg!.Frequency);
        Assert.Equal(15, cfg.DayOfMonth);
        Assert.Equal(4, cfg.HourUtc);
        Assert.Equal(30, cfg.MinuteUtc);
        Assert.Equal(cron, BackupScheduleCronCodec.BuildCron(cfg));
    }
}
