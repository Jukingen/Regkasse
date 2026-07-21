using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Hosted;

/// <summary>
/// Periodically trims <c>elmah_error</c> to <see cref="ElmahOptions.MaxLogEntries"/> (oldest rows first).
/// </summary>
public sealed class ElmahRetentionHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ElmahOptions _options;
    private readonly ILogger<ElmahRetentionHostedService> _logger;

    public ElmahRetentionHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<ElmahOptions> options,
        ILogger<ElmahRetentionHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = Math.Max(5, _options.RetentionCheckIntervalMinutes);
        var interval = TimeSpan.FromMinutes(intervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunRetentionSweepAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Elmah retention sweep failed.");
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

    private async Task RunRetentionSweepAsync(CancellationToken cancellationToken)
    {
        if (_options.MaxLogEntries <= 0)
            return;

        var applicationName = string.IsNullOrWhiteSpace(_options.ApplicationName)
            ? "Regkasse"
            : _options.ApplicationName.Trim();

        await using var scope = _scopeFactory.CreateAsyncScope();
        var queryService = scope.ServiceProvider.GetRequiredService<IElmahErrorQueryService>();
        var deleted = await queryService
            .EnforceMaxEntriesAsync(applicationName, _options.MaxLogEntries, cancellationToken)
            .ConfigureAwait(false);

        if (deleted > 0)
        {
            _logger.LogInformation(
                "Elmah retention deleted {DeletedCount} row(s) for application {ApplicationName} (max={MaxLogEntries}).",
                deleted,
                applicationName,
                _options.MaxLogEntries);
        }
    }
}
