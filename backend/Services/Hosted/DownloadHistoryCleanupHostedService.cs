using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Hosted;

/// <summary>Deletes <c>download_history</c> rows older than the configured retention window.</summary>
public sealed class DownloadHistoryCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DownloadHistoryOptions _options;
    private readonly ILogger<DownloadHistoryCleanupHostedService> _logger;

    public DownloadHistoryCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<DownloadHistoryOptions> options,
        ILogger<DownloadHistoryCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = Math.Max(60, _options.CleanupIntervalMinutes);
        var interval = TimeSpan.FromMinutes(intervalMinutes);

        // Stagger first run slightly after startup.
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
                await RunCleanupAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Download history cleanup failed.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RunCleanupAsync(CancellationToken cancellationToken)
    {
        if (!_options.CleanupEnabled)
            return;

        var retentionDays = Math.Clamp(_options.RetentionDays, 1, 365);
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IDownloadHistoryService>();
        var deleted = await service.CleanupOlderThanAsync(cutoff, cancellationToken).ConfigureAwait(false);

        if (deleted > 0)
        {
            _logger.LogInformation(
                "Download history retention deleted {DeletedCount} row(s) (retentionDays={RetentionDays}).",
                deleted,
                retentionDays);
        }
    }
}

