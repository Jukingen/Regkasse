using KasseAPI_Final.Services.FinanzOnlineIntegration;

namespace KasseAPI_Final.Services.Hosted;

/// <summary>
/// Every five minutes, alerts Slack/email when FinanzOnline outbox failures in the last hour exceed 5.
/// One alert per UTC hour while the condition holds.
/// </summary>
public sealed class FinanzOnlineOutboxAlertHostedService : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FinanzOnlineOutboxAlertHostedService> _logger;

    public FinanzOnlineOutboxAlertHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<FinanzOnlineOutboxAlertHostedService> logger)
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
                _logger.LogError(ex, "FinanzOnline outbox alert check failed.");
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
