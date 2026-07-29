using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

/// <summary>
/// Daily DEP export compliance reminder sweep (activity feed + configured email/webhook).
/// Distinct from cron export runner <see cref="DepExportSchedulerHostedService"/>.
/// </summary>
public sealed class DepExportReminderHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<DepExportReminderOptions> _options;
    private readonly ILogger<DepExportReminderHostedService> _logger;

    public DepExportReminderHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<DepExportReminderOptions> options,
        ILogger<DepExportReminderHostedService> logger)
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
                var reminder = scope.ServiceProvider.GetRequiredService<IDepExportReminderService>();
                await reminder.CheckAndNotifyAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "DEP export reminder hosted service iteration failed.");
            }
        }
    }
}
