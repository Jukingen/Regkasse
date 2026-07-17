using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Periodic cost/ops monitoring: alerts when succeeded logical-dump storage reaches
/// <see cref="AlertThresholdPercent"/> of <see cref="BackupService.MaxStorageBytes"/> (~10 GB),
/// or when staging volume usage hits <see cref="BackupOptions.StagingDiskUsageAlertPercent"/>.
/// </summary>
public sealed class StorageAlertService : BackgroundService
{
    /// <summary>Alert when used dump budget reaches this percent of <see cref="BackupService.MaxStorageBytes"/>.</summary>
    public const int AlertThresholdPercent = 80;

    private static readonly TimeSpan MinCheckInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<BackupOptions> _options;
    private readonly IBackupStagingDiskMonitor _diskMonitor;
    private readonly IBackupAlertPublisher _alerts;
    private readonly ILogger<StorageAlertService> _logger;

    public StorageAlertService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<BackupOptions> options,
        IBackupStagingDiskMonitor diskMonitor,
        IBackupAlertPublisher alerts,
        ILogger<StorageAlertService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _diskMonitor = diskMonitor;
        _alerts = alerts;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckStorageAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backup storage alert check failed");
            }

            var delay = _options.CurrentValue.StorageAlertCheckInterval;
            if (delay < MinCheckInterval)
                delay = MinCheckInterval;

            try
            {
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    /// <summary>One monitoring tick (test hook via InternalsVisibleTo).</summary>
    internal async Task CheckStorageAsync(CancellationToken cancellationToken)
    {
        var opts = _options.CurrentValue;
        long usedBytes;

        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            usedBytes = await (
                    from a in db.BackupArtifacts.AsNoTracking()
                    join r in db.BackupRuns.AsNoTracking() on a.BackupRunId equals r.Id
                    where a.ArtifactType == BackupArtifactType.LogicalDump
                          && a.ByteSize != null
                          && r.Status == BackupRunStatus.Succeeded
                    select a.ByteSize!.Value)
                .SumAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var maxBytes = BackupService.MaxStorageBytes;
        var budgetAlertBytes = maxBytes * AlertThresholdPercent / 100L;
        if (usedBytes >= budgetAlertBytes)
        {
            var usedPercent = maxBytes <= 0
                ? 100.0
                : Math.Round(100.0 * usedBytes / maxBytes, 1);
            _alerts.Publish(new BackupAlertEvent(
                BackupAlertKind.StoragePressure,
                BackupRunId: null,
                CorrelationId: null,
                Message:
                $"Backup storage budget at {usedPercent}% ({usedBytes} / {maxBytes} bytes). Alert threshold {AlertThresholdPercent}%.",
                Data: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["reason"] = "storage_budget",
                    ["usedBytes"] = usedBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["maxStorageBytes"] = maxBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["usedPercent"] = usedPercent.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["alertThresholdPercent"] = AlertThresholdPercent.ToString(System.Globalization.CultureInfo.InvariantCulture),
                }));
        }

        var disk = _diskMonitor.TryGetUsage(opts.ArtifactStagingRoot, opts.StagingDiskUsageAlertPercent);
        if (disk is { Alert: true })
        {
            _alerts.Publish(new BackupAlertEvent(
                BackupAlertKind.StoragePressure,
                BackupRunId: null,
                CorrelationId: null,
                Message:
                $"Backup staging disk at {disk.UsedPercent}% (alert threshold {opts.StagingDiskUsageAlertPercent}%). Free space before further dumps.",
                Data: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["reason"] = "staging_disk",
                    ["usedPercent"] = disk.UsedPercent.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["alertThresholdPercent"] = opts.StagingDiskUsageAlertPercent
                        .ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["availableBytes"] = disk.AvailableBytes
                        .ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["totalBytes"] = disk.TotalBytes
                        .ToString(System.Globalization.CultureInfo.InvariantCulture),
                }));
        }
    }
}
