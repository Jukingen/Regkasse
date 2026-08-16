using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Trial;

/// <summary>Sends trial reminder / expiry emails on a fixed interval (default every 6 hours).</summary>
public sealed class TrialReminderHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<TrialOptions> _options;
    private readonly ILogger<TrialReminderHostedService> _logger;

    public TrialReminderHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<TrialOptions> options,
        ILogger<TrialReminderHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (OpenApiExportMode.IsEnabled)
            return;

        // Stagger startup slightly so boot storms settle.
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
                await RunCycleAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Trial reminder cycle failed.");
            }

            var hours = Math.Clamp(_options.CurrentValue.ReminderIntervalHours, 1, 24);
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

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        if (!_options.CurrentValue.Enabled)
            return;

        using var scope = _scopeFactory.CreateScope();
        var trial = scope.ServiceProvider.GetRequiredService<ITrialService>();
        var expired = await trial.ProcessExpiryAndGraceAsync(cancellationToken).ConfigureAwait(false);
        var reminders = await trial.ProcessRemindersAsync(cancellationToken).ConfigureAwait(false);
        if (expired > 0 || reminders > 0)
        {
            _logger.LogInformation(
                "Trial reminder cycle: expiredMarked={Expired} remindersSent={Reminders}",
                expired,
                reminders);
        }
    }
}
