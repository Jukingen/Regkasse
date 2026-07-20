using KasseAPI_Final.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.DataRetention;

/// <summary>
/// Daily (configurable) RKSV cold-archive sweep. Uses <see cref="IServiceScopeFactory"/>
/// because hosted services are singletons.
/// </summary>
public sealed class RksvDataCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<RksvDataCleanupOptions> _options;
    private readonly ILogger<RksvDataCleanupHostedService> _logger;

    public RksvDataCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<RksvDataCleanupOptions> options,
        ILogger<RksvDataCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var graceMinutes = Math.Max(0, _options.CurrentValue.StartupGraceMinutes);
        if (graceMinutes > 0)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(graceMinutes), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var opts = _options.CurrentValue;
            if (opts.Enabled)
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var cleanup = scope.ServiceProvider.GetRequiredService<IRksvDataCleanupService>();
                    await cleanup.CleanupRksvDataAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "RKSV data cleanup failed");
                }
            }

            var hours = Math.Max(1, opts.IntervalHours);
            try
            {
                await Task.Delay(TimeSpan.FromHours(hours), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
