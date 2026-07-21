using KasseAPI_Final.Services.Billing;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Hosted;

public sealed class BillingBackupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BillingBackupConfig _config;
    private readonly ILogger<BillingBackupHostedService> _logger;

    public BillingBackupHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<BillingBackupConfig> config,
        ILogger<BillingBackupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.Enabled)
        {
            _logger.LogInformation("Billing backup hosted service is disabled");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var delay = GetDelayUntilNextRun(DateTime.UtcNow);
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);

                var runAt = DateTime.UtcNow;
                using var scope = _scopeFactory.CreateScope();
                var backupService = scope.ServiceProvider.GetRequiredService<IBillingBackupService>();

                var dailyDate = runAt.Date.AddDays(-1);
                await backupService.BackupDailyAsync(dailyDate, triggeredByUserId: null, stoppingToken)
                    .ConfigureAwait(false);

                if (runAt.DayOfWeek == DayOfWeek.Sunday)
                {
                    var weekStart = runAt.Date.AddDays(-7);
                    await backupService.BackupWeeklyAsync(weekStart, triggeredByUserId: null, stoppingToken)
                        .ConfigureAwait(false);
                }

                if (runAt.Day == 1)
                {
                    await backupService.CleanupExpiredBackupsAsync(stoppingToken).ConfigureAwait(false);
                }

                _logger.LogInformation("Scheduled billing backup completed for {Date}", dailyDate);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in billing backup background service");
            }
        }
    }

    private TimeSpan GetDelayUntilNextRun(DateTime utcNow)
    {
        var hour = Math.Clamp(_config.DailyBackupHourUtc, 0, 23);
        var nextRun = utcNow.Date.AddHours(hour);
        if (utcNow >= nextRun)
            nextRun = nextRun.AddDays(1);

        return nextRun - utcNow;
    }
}
