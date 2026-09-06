using KasseAPI_Final.Configuration;
using KasseAPI_Final.DTOs;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Filesystem inventory of WAL segments. Postgres must copy files here via <c>archive_command</c>.
/// </summary>
public sealed class WalArchiveService : IWalArchiveService
{
    public const string DefaultRelativeDirectory = "App_Data/wal-archive";

    private readonly IOptionsMonitor<BackupOptions> _options;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly TimeProvider _time;
    private readonly ILogger<WalArchiveService> _logger;

    public WalArchiveService(
        IOptionsMonitor<BackupOptions> options,
        IHostEnvironment hostEnvironment,
        TimeProvider time,
        ILogger<WalArchiveService> logger)
    {
        _options = options;
        _hostEnvironment = hostEnvironment;
        _time = time;
        _logger = logger;
    }

    public WalArchiveStatusDto GetStatus()
    {
        var opts = _options.CurrentValue;
        var directory = ResolveDirectory(opts);
        var exists = Directory.Exists(directory);
        var files = exists ? EnumerateFiles(directory) : Array.Empty<WalArchiveFileInfo>();
        var oldest = files.Count > 0 ? files.Min(f => f.LastWriteUtc) : (DateTime?)null;
        var newest = files.Count > 0 ? files.Max(f => f.LastWriteUtc) : (DateTime?)null;
        var enabled = opts.PitrWalArchivingDeclaredEnabled || files.Count > 0;
        var lag = opts.PitrWalArchiveDeclaredLagMinutes;
        if (enabled && lag is null or < 0)
            lag = Math.Max(1, opts.WalArchiveSwitchIntervalMinutes);

        var message = !exists
            ? "WAL archive directory is missing. Configure PostgreSQL archive_command to copy segments here."
            : files.Count == 0
                ? "WAL archive directory is empty. archive_mode/archive_command is not delivering files yet."
                : $"WAL archive contains {files.Count} file(s); retention {opts.WalArchiveRetentionDays} day(s).";

        return new WalArchiveStatusDto
        {
            Enabled = enabled,
            DirectoryExists = exists,
            Directory = directory,
            FileCount = files.Count,
            OldestFileUtc = oldest,
            NewestFileUtc = newest,
            RetentionDays = Math.Max(1, opts.WalArchiveRetentionDays),
            SwitchIntervalMinutes = Math.Max(1, opts.WalArchiveSwitchIntervalMinutes),
            LagMinutes = lag,
            HostArchiveCommandRequired = true,
            Message = message
        };
    }

    public IReadOnlyList<WalArchiveFileInfo> ListFiles(int take = 200)
    {
        var directory = ResolveDirectory(_options.CurrentValue);
        if (!Directory.Exists(directory))
            return Array.Empty<WalArchiveFileInfo>();

        var limit = Math.Clamp(take, 1, 2000);
        return EnumerateFiles(directory)
            .OrderByDescending(f => f.LastWriteUtc)
            .Take(limit)
            .ToList();
    }

    public int PurgeExpired()
    {
        var opts = _options.CurrentValue;
        var directory = ResolveDirectory(opts);
        if (!Directory.Exists(directory))
            return 0;

        var retentionDays = Math.Max(1, opts.WalArchiveRetentionDays);
        var cutoff = _time.GetUtcNow().UtcDateTime.AddDays(-retentionDays);
        var deleted = 0;
        foreach (var file in EnumerateFiles(directory))
        {
            if (file.LastWriteUtc >= cutoff)
                continue;

            var path = Path.Combine(directory, file.FileName);
            try
            {
                File.Delete(path);
                deleted++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "WAL archive retention could not delete {Path}", path);
            }
        }

        if (deleted > 0)
        {
            _logger.LogInformation(
                "WAL archive retention deleted {Count} file(s) older than {Cutoff:O} from {Directory}",
                deleted,
                cutoff,
                directory);
        }

        return deleted;
    }

    public bool CoversWindow(DateTime fromUtc, DateTime toUtc)
    {
        var status = GetStatus();
        if (!status.Enabled)
            return false;

        if (status.FileCount == 0)
            return _options.CurrentValue.PitrWalArchivingDeclaredEnabled && toUtc > fromUtc;

        if (!status.OldestFileUtc.HasValue || !status.NewestFileUtc.HasValue)
            return false;

        var start = fromUtc.Kind == DateTimeKind.Utc ? fromUtc : DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc);
        var end = toUtc.Kind == DateTimeKind.Utc ? toUtc : DateTime.SpecifyKind(toUtc, DateTimeKind.Utc);
        if (end <= start)
            return false;

        return status.OldestFileUtc.Value <= start && status.NewestFileUtc.Value >= end.AddMinutes(-1);
    }

    internal string ResolveDirectory(BackupOptions opts)
    {
        if (!string.IsNullOrWhiteSpace(opts.WalArchiveDirectory))
            return Path.GetFullPath(opts.WalArchiveDirectory);

        return Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, DefaultRelativeDirectory));
    }

    private static IReadOnlyList<WalArchiveFileInfo> EnumerateFiles(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory)
                .Select(path =>
                {
                    var info = new FileInfo(path);
                    return new WalArchiveFileInfo(
                        info.Name,
                        info.Length,
                        DateTime.SpecifyKind(info.LastWriteTimeUtc, DateTimeKind.Utc));
                })
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<WalArchiveFileInfo>();
        }
    }
}
