using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupScheduleProjectionHelperTests
{
    [Fact]
    public void IsScheduleDue_true_when_next_fire_before_now()
    {
        var row = new BackupScheduleConfiguration
        {
            Enabled = true,
            ScheduleCron = "0 2 * * *",
            LastRunAt = DateTime.UtcNow.AddDays(-2),
        };
        Assert.True(BackupScheduleProjectionHelper.IsScheduleDue(row, DateTime.UtcNow));
    }

    [Fact]
    public void IsScheduleDue_false_when_disabled()
    {
        var row = new BackupScheduleConfiguration
        {
            Enabled = false,
            ScheduleCron = "0 2 * * *",
            LastRunAt = DateTime.UtcNow.AddDays(-2),
        };
        Assert.False(BackupScheduleProjectionHelper.IsScheduleDue(row, DateTime.UtcNow));
    }
}
