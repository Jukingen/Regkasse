using Cronos;
using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Daily Tenant incremental enqueue after the last succeeded full Tenant backup.
/// Opt-in via <c>Backup:IncrementalBackupEnabled</c>.
/// </summary>
public sealed class IncrementalBackupScheduledEnqueueService : BackgroundService
{
    private static readonly TimeSpan StartupGrace = TimeSpan.FromMinutes(4);
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<BackupOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<IncrementalBackupScheduledEnqueueService> _logger;
    private DateTime? _lastEnqueueUtc;

    public IncrementalBackupScheduledEnqueueService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<BackupOptions> options,
        TimeProvider time,
        ILogger<IncrementalBackupScheduledEnqueueService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupGrace, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TryEnqueueAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Daily incremental backup enqueue failed.");
            }

            try
            {
                await Task.Delay(Tick, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task TryEnqueueAsync(CancellationToken cancellationToken)
    {
        var opts = _options.CurrentValue;
        if (!opts.IncrementalBackupEnabled || !opts.WorkerEnabled)
            return;

        if (!CronExpression.TryParse(opts.IncrementalBackupCron, CronFormat.Standard, out var expr))
        {
            _logger.LogWarning("Incremental backup cron failed to parse: {Cron}", opts.IncrementalBackupCron);
            return;
        }

        var utcNow = _time.GetUtcNow().UtcDateTime;
        var anchor = _lastEnqueueUtc ?? utcNow.AddDays(-2);
        var next = expr.GetNextOccurrence(anchor, TimeZoneInfo.Utc, inclusive: false);
        if (next == null || next.Value > utcNow)
            return;

        using var scope = _scopeFactory.CreateScope();
        var incremental = scope.ServiceProvider.GetRequiredService<IIncrementalBackupService>();
        var count = await incremental.EnqueueDueDailyIncrementalsAsync(cancellationToken).ConfigureAwait(false);
        _lastEnqueueUtc = utcNow;
        _logger.LogInformation("Daily incremental enqueue finished: {Count} run(s).", count);
    }
}
