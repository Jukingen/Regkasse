using KasseAPI_Final.Services.FinanzOnlineIntegration;

namespace KasseAPI_Final.Services.Hosted;

/// <summary>
/// Every 15 minutes, alerts Slack/email when FinanzOnline outbox failures in the last hour exceed 5.
/// One alert per UTC hour while the condition holds. Does not submit SOAP.
/// </summary>
public sealed class FinanzOnlineOutboxMonitorHostedService : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FinanzOnlineOutboxMonitorHostedService> _logger;

    public FinanzOnlineOutboxMonitorHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<FinanzOnlineOutboxMonitorHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (OpenApiExportMode.IsEnabled)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var monitor = scope.ServiceProvider.GetRequiredService<IFinanzOnlineOutboxFailureMonitor>();
                await monitor.CheckAndAlertAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "FinanzOnline outbox monitor check failed.");
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
