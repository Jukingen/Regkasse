using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

/// <summary>
/// Periodic hot-storage / download-token / stale-metadata cleanup for DEP exports.
/// Does <strong>not</strong> hard-delete completed fiscal archives before the RKSV retention window
/// (that remains <see cref="DepExportArchiveHostedService"/> + <see cref="IDepExportArchiveService"/>).
/// </summary>
public sealed class DepExportCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<DepExportStorageOptions> _options;
    private readonly ILogger<DepExportCleanupHostedService> _logger;

    public DepExportCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<DepExportStorageOptions> options,
        ILogger<DepExportCleanupHostedService> logger)
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
                var hours = Math.Max(1, _options.CurrentValue.CleanupIntervalHours);
                await Task.Delay(TimeSpan.FromHours(hours), stoppingToken).ConfigureAwait(false);

                if (!_options.CurrentValue.CleanupEnabled)
                    continue;

                await CleanupOldExportsAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Failed to cleanup old DEP exports.");
            }
        }
    }

    private async Task CleanupOldExportsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var history = scope.ServiceProvider.GetRequiredService<IDepExportHistoryService>();
        var result = await history.CleanupExpiredStorageAsync(cancellationToken).ConfigureAwait(false);

        if (result.HotFilesDeleted > 0 ||
            result.TokensCleared > 0 ||
            result.MetadataRowsDeleted > 0 ||
            result.FailedCount > 0)
        {
            _logger.LogInformation(
                "DEP storage cleanup deleted={HotDeleted} tokensCleared={Tokens} metadataDeleted={Meta} failed={Failed} cutoff={Cutoff:o}",
                result.HotFilesDeleted,
                result.TokensCleared,
                result.MetadataRowsDeleted,
                result.FailedCount,
                result.CutoffUtc);
        }
    }
}
