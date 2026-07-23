using KasseAPI_Final.Services;

namespace KasseAPI_Final.Services.Hosted;

/// <summary>Periodically snapshots permission usage analytics (every 6 hours).</summary>
public sealed class PermissionAnalyticsSnapshotHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PermissionAnalyticsSnapshotHostedService> _logger;

    public PermissionAnalyticsSnapshotHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<PermissionAnalyticsSnapshotHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (OpenApiExportMode.IsEnabled)
            return;

        // Initial delay so startup is not blocked by a heavy walk.
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var analytics = scope.ServiceProvider.GetRequiredService<IPermissionAnalyticsService>();
                await analytics.SnapshotTodayAsync(stoppingToken).ConfigureAwait(false);
                _logger.LogInformation("Permission analytics daily snapshot completed");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Permission analytics snapshot failed");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
