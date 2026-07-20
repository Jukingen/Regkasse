using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services.DataRights;

/// <summary>
/// Retries pending GDPR export requests so they complete within the 24-hour SLA.
/// Uses <see cref="IServiceScopeFactory"/> (singleton hosted service).
/// </summary>
public sealed class DataRightsExportProcessorService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DataRightsExportProcessorService> _logger;

    public DataRightsExportProcessorService(
        IServiceScopeFactory scopeFactory,
        ILogger<DataRightsExportProcessorService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
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
                var rights = scope.ServiceProvider.GetRequiredService<ICustomerDataRightsService>();
                var count = await rights.ProcessPendingExportsAsync(stoppingToken).ConfigureAwait(false);
                if (count > 0)
                {
                    _logger.LogInformation(
                        "DataRightsExportProcessorService processed {Count} export request(s).",
                        count);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "DataRightsExportProcessorService iteration failed.");
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
