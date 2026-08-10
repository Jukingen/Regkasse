using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Services.License;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Hosted;

/// <summary>
/// Daily Super Admin billing reminder sweep: create due <c>license_reminders</c> rows, then send emails.
/// Tick time comes from <see cref="BillingOptions.ReminderCheckHourUtc"/> / <see cref="BillingOptions.ReminderCheckMinuteUtc"/>.
/// </summary>
public class BillingReminderHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<BillingOptions> _billingOptions;
    private readonly ILogger<BillingReminderHostedService> _logger;

    public BillingReminderHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<BillingOptions> billingOptions,
        ILogger<BillingReminderHostedService> logger)
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
                var delay = ComputeDelayUntilUtc(opt.ReminderCheckHourUtc, opt.ReminderCheckMinuteUtc);
                _logger.LogDebug(
                    "Billing reminder next tick in {Delay} (target {Hour:D2}:{Minute:D2} UTC)",
                    delay,
                    Math.Clamp(opt.ReminderCheckHourUtc, 0, 23),
                    Math.Clamp(opt.ReminderCheckMinuteUtc, 0, 59));

                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);

                using var scope = _scopeFactory.CreateScope();
                var reminderService = scope.ServiceProvider.GetRequiredService<IReminderService>();
                var licenseReminderService = scope.ServiceProvider.GetRequiredService<ILicenseReminderService>();

                await reminderService.CheckAndCreateRemindersAsync(stoppingToken).ConfigureAwait(false);
                var sent = await licenseReminderService
                    .SendDueBillingSaleRemindersAsync(stoppingToken)
                    .ConfigureAwait(false);

                if (sent > 0)
                {
                    _logger.LogInformation(
                        "Billing reminder sweep completed: emailsSent={EmailsSent}",
                        sent);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in billing reminder background service");
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    /// <summary>Public for unit tests — delay until next UTC clock match.</summary>
    public static TimeSpan ComputeDelayUntilUtc(int hourUtc, int minuteUtc)
    {
        hourUtc = Math.Clamp(hourUtc, 0, 23);
        minuteUtc = Math.Clamp(minuteUtc, 0, 59);

        var now = DateTime.UtcNow;
        var next = new DateTime(now.Year, now.Month, now.Day, hourUtc, minuteUtc, 0, DateTimeKind.Utc);
        if (next <= now)
            next = next.AddDays(1);

        return next - now;
    }
}
