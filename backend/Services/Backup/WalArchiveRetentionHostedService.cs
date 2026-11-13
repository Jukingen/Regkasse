namespace KasseAPI_Final.Services.Backup;

/// <summary>Periodic WAL archive file rotation (default every hour).</summary>
public sealed class WalArchiveRetentionHostedService : BackgroundService
{
    private static readonly TimeSpan StartupGrace = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WalArchiveRetentionHostedService> _logger;

    public WalArchiveRetentionHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<WalArchiveRetentionHostedService> logger)
    {
        _scopeFactory = scopeFactory;
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
                using var scope = _scopeFactory.CreateScope();
                var wal = scope.ServiceProvider.GetRequiredService<IWalArchiveService>();
                wal.PurgeExpired();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WAL archive retention pass failed.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
