namespace KasseAPI_Final.Services.Backup;

/// <summary>Staging / archive volume usage for cost monitoring (80% alert default).</summary>
public sealed class BackupStagingDiskUsage
{
    public string RootPath { get; init; } = string.Empty;
    public long TotalBytes { get; init; }
    public long AvailableBytes { get; init; }
    public double UsedPercent { get; init; }
    public bool Alert { get; init; }
}

public interface IBackupStagingDiskMonitor
{
    /// <summary>
    /// Returns usage for <paramref name="stagingRoot"/> when the path exists and the volume can be measured;
    /// otherwise null (e.g. unset root, Fake adapter, or unsupported mount).
    /// </summary>
    BackupStagingDiskUsage? TryGetUsage(string? stagingRoot, int alertPercent);
}

public sealed class BackupStagingDiskMonitor : IBackupStagingDiskMonitor
{
    public BackupStagingDiskUsage? TryGetUsage(string? stagingRoot, int alertPercent)
    {
        if (string.IsNullOrWhiteSpace(stagingRoot))
            return null;

        string full;
        try
        {
            full = Path.GetFullPath(stagingRoot.Trim());
        }
        catch
        {
            return null;
        }

        DriveInfo? drive;
        try
        {
            drive = new DriveInfo(Path.GetPathRoot(full) ?? full);
            if (!drive.IsReady)
                return null;
        }
        catch
        {
            return null;
        }

        var total = drive.TotalSize;
        if (total <= 0)
            return null;

        var available = drive.AvailableFreeSpace;
        var usedPercent = Math.Round(100.0 * (1.0 - (double)available / total), 1);
        var threshold = Math.Clamp(alertPercent, 1, 99);

        return new BackupStagingDiskUsage
        {
            RootPath = full,
            TotalBytes = total,
            AvailableBytes = available,
            UsedPercent = usedPercent,
            Alert = usedPercent >= threshold,
        };
    }
}
