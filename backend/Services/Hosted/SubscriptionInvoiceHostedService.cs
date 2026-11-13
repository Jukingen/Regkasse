using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.Billing;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Hosted;

/// <summary>Monthly SaaS subscription invoice generation for active paid tenants.</summary>
public sealed class SubscriptionInvoiceHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<BillingOptions> _billingOptions;
    private readonly ILogger<SubscriptionInvoiceHostedService> _logger;

    public SubscriptionInvoiceHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<BillingOptions> billingOptions,
        ILogger<SubscriptionInvoiceHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _billingOptions = billingOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var opt = _billingOptions.CurrentValue;
                if (!opt.AutoMonthlyInvoicingEnabled)
                {
                    await Task.Delay(TimeSpan.FromHours(6), stoppingToken).ConfigureAwait(false);
                    continue;
                }

                var delay = BillingReminderHostedService.ComputeDelayUntilUtc(
                    opt.MonthlyInvoiceHourUtc,
                    opt.MonthlyInvoiceMinuteUtc);
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);

                // Run only on the configured day of month (default: 1st).
                if (DateTime.UtcNow.Day != Math.Clamp(opt.MonthlyInvoiceDayOfMonth, 1, 28))
                    continue;

                using var scope = _scopeFactory.CreateScope();
                var invoices = scope.ServiceProvider.GetRequiredService<ISubscriptionInvoiceService>();
                var result = await invoices.GenerateMonthlyInvoicesAsync(cancellationToken: stoppingToken)
                    .ConfigureAwait(false);

                _logger.LogInformation(
                    "Subscription invoice sweep: created={Created} skipped={Skipped} failed={Failed}",
                    result.Created,
                    result.Skipped,
                    result.Failed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in subscription invoice background service");
                try
                {
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }
}
