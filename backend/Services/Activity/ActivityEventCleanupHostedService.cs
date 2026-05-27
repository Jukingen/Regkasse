using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Activity;

/// <summary>Deletes activity events older than <see cref="ActivityNotificationOptions.EventRetentionDays"/>.</summary>
public sealed class ActivityEventCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<ActivityNotificationOptions> _options;
    private readonly ILogger<ActivityEventCleanupHostedService> _logger;

    public ActivityEventCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<ActivityNotificationOptions> options,
        ILogger<ActivityEventCleanupHostedService> logger)
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
                await PurgeOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Activity event retention purge failed");
            }

            await Task.Delay(TimeSpan.FromHours(6), stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task PurgeOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var activity = scope.ServiceProvider.GetRequiredService<IActivityEventService>();
        var removed = await activity.PurgeExpiredAsync(cancellationToken).ConfigureAwait(false);
        if (removed > 0)
        {
            _logger.LogInformation(
                "Activity event retention removed {Count} event(s) older than {Days} day(s)",
                removed,
                Math.Max(1, _options.CurrentValue.EventRetentionDays));
        }
    }
}
