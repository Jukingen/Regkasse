using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

/// <summary>
/// Periodic DEP export archive backfill + 7-year retention purge.
/// Distinct from cron export runner and compliance reminder workers.
/// </summary>
public sealed class DepExportArchiveHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<DepExportArchiveOptions> _options;
    private readonly ILogger<DepExportArchiveHostedService> _logger;

    public DepExportArchiveHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<DepExportArchiveOptions> options,
        ILogger<DepExportArchiveHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
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
                var hours = Math.Max(1, _options.CurrentValue.CheckIntervalHours);
                await Task.Delay(TimeSpan.FromHours(hours), stoppingToken).ConfigureAwait(false);

                if (!_options.CurrentValue.Enabled)
                    continue;

                await using var scope = _scopeFactory.CreateAsyncScope();
                var archive = scope.ServiceProvider.GetRequiredService<IDepExportArchiveService>();

                var archived = await archive.ArchivePendingExportsAsync(stoppingToken)
                    .ConfigureAwait(false);
                if (archived > 0)
                {
                    _logger.LogInformation(
                        "DEP archive sweep archived {Count} pending export(s).",
                        archived);
                }

                if (_options.CurrentValue.PurgeEnabled)
                {
                    var purge = await archive.PurgeOldExportsAsync(cancellationToken: stoppingToken)
                        .ConfigureAwait(false);
                    if (purge.PurgedCount > 0 || purge.FailedCount > 0)
                    {
                        _logger.LogInformation(
                            "DEP archive purge examined={Examined} purged={Purged} failed={Failed} cutoff={Cutoff:o}",
                            purge.ExaminedCount,
                            purge.PurgedCount,
                            purge.FailedCount,
                            purge.CutoffUtc);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "DEP export archive hosted service iteration failed.");
            }
        }
    }
}
