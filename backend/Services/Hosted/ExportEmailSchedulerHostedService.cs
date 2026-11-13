using KasseAPI_Final.Services;

namespace KasseAPI_Final.Services.Hosted;

/// <summary>Processes due scheduled export-email deliveries.</summary>
public sealed class ExportEmailSchedulerHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExportEmailSchedulerHostedService> _logger;

    public ExportEmailSchedulerHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<ExportEmailSchedulerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var delivery = scope.ServiceProvider.GetRequiredService<IExportEmailDeliveryService>();
                var sent = await delivery.ProcessDueSchedulesAsync(stoppingToken).ConfigureAwait(false);
                if (sent > 0)
                    _logger.LogInformation("Processed {Count} scheduled export emails.", sent);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Export email scheduler tick failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
