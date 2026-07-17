using KasseAPI_Final.Services.Backup;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupStagingDiskMonitorTests
{
    [Fact]
    public void TryGetUsage_returns_null_when_root_unset()
    {
        var sut = new BackupStagingDiskMonitor();
        Assert.Null(sut.TryGetUsage(null, 80));
        Assert.Null(sut.TryGetUsage("  ", 80));
    }

    [Fact]
    public void TryGetUsage_reports_percent_for_existing_temp_path()
    {
        var sut = new BackupStagingDiskMonitor();
        var usage = sut.TryGetUsage(Path.GetTempPath(), alertPercent: 80);
        if (usage == null)
            return; // some CI mounts may not expose DriveInfo

        Assert.InRange(usage.UsedPercent, 0, 100);
        Assert.True(usage.TotalBytes > 0);
        Assert.Equal(usage.UsedPercent >= 80, usage.Alert);
    }
}

public sealed class BackupSettingsRetentionBoundsTests
{
    [Fact]
    public void Retention_bounds_are_cost_optimized_7_to_90()
    {
        Assert.Equal(7, BackupSettingsAdminService.MinRetentionDays);
        Assert.Equal(90, BackupSettingsAdminService.MaxRetentionDays);
    }
}
