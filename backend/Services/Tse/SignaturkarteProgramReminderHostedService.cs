using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Tse;

/// <summary>Daily Mai 2027 Signaturkarte program reminder sweep (activity + configured email/webhook).</summary>
public sealed class SignaturkarteProgramReminderHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<SignaturkarteProgramOptions> _options;
    private readonly ILogger<SignaturkarteProgramReminderHostedService> _logger;

    public SignaturkarteProgramReminderHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<SignaturkarteProgramOptions> options,
        ILogger<SignaturkarteProgramReminderHostedService> logger)
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
                var reminder = scope.ServiceProvider.GetRequiredService<ISignaturkarteProgramReminderService>();
                await reminder.CheckAndNotifyAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Signaturkarte program reminder hosted service iteration failed.");
            }
        }
    }
}
