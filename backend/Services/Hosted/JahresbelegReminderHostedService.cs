using KasseAPI_Final.Configuration;

namespace KasseAPI_Final.Services.Hosted;

/// <summary>
/// Daily sweep for the 1 December Jahresbeleg warning. Other days the check publishes nothing.
/// </summary>
public sealed class JahresbelegReminderHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JahresbelegReminderHostedService> _logger;

    public JahresbelegReminderHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<JahresbelegReminderHostedService> logger)
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
                var reminder = scope.ServiceProvider.GetRequiredService<IJahresbelegReminderService>();
                var sent = await reminder.CheckAndNotifyAsync(stoppingToken).ConfigureAwait(false);
                if (sent > 0)
                {
                    _logger.LogInformation(
                        "Jahresbeleg reminder hosted service published {Count} warning(s).",
                        sent);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Jahresbeleg reminder hosted service iteration failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
