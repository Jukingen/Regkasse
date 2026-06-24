using KasseAPI_Final.Services.Billing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services.Hosted;

public class BillingReminderHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BillingReminderHostedService> _logger;

    public BillingReminderHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<BillingReminderHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken).ConfigureAwait(false);

                using var scope = _scopeFactory.CreateScope();
                var reminderService = scope.ServiceProvider.GetRequiredService<IReminderService>();

                await reminderService.CheckAndCreateRemindersAsync(stoppingToken).ConfigureAwait(false);
                await reminderService.SendPendingRemindersAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in billing reminder background service");
            }
        }
    }
}
