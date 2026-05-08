using KasseAPI_Final;
using KasseAPI_Final.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

/// <summary>
/// Daily license reminder sweep (UTC): updates in-app reminder store, optionally emails when urgent,
/// and logs critical alerts when expired. Uses <see cref="Timer"/>-driven scheduling from <see cref="IHostedService"/>.
/// </summary>
public sealed class LicenseReminderHostedService : IHostedService, IDisposable
{
    /// <summary>Implicit in-app escalation window (German UI consumes English API copy).</summary>
    private const int InAppExpiryDayThreshold = 15;

    private readonly IOptionsMonitor<LicenseOptions> _licenseOptions;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LicenseReminderHostedService> _logger;
    private readonly object _timerGate = new();
    private Timer? _timer;

    public LicenseReminderHostedService(
        IOptionsMonitor<LicenseOptions> licenseOptions,
        IServiceScopeFactory scopeFactory,
        ILogger<LicenseReminderHostedService> logger)
    {
        _licenseOptions = licenseOptions;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (OpenApiExportMode.IsEnabled)
            return Task.CompletedTask;

        ScheduleNext(reason: "host_start");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        lock (_timerGate)
        {
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        lock (_timerGate)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }

    private void ScheduleNext(string reason)
    {
        var opt = _licenseOptions.CurrentValue;
        var delay = ComputeDelayUntilUtc(opt.ReminderCheckHourUtc, opt.ReminderCheckMinuteUtc);
        lock (_timerGate)
        {
            _timer?.Dispose();
            _timer = new Timer(
                _ => RunTickFireAndForget(),
                null,
                dueTime: delay,
                period: Timeout.InfiniteTimeSpan);
        }

        _logger.LogDebug(
            "License reminder timer armed: reason={Reason} nextUtcIn={Delay} targetHourUtc={Hour} minuteUtc={Minute}",
            reason,
            delay,
            Math.Clamp(opt.ReminderCheckHourUtc, 0, 23),
            Math.Clamp(opt.ReminderCheckMinuteUtc, 0, 59));
    }

    private void RunTickFireAndForget()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await RunCycleAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "License reminder evaluation failed.");
            }
            finally
            {
                try
                {
                    ScheduleNext(reason: "after_tick");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "License reminder timer could not be rescheduled.");
                }
            }
        });
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        var lic = sp.GetRequiredService<ILicenseService>();
        var store = sp.GetRequiredService<ILicenseReminderNotificationStore>();
        var mail = sp.GetRequiredService<ILicenseReminderEmailSender>();
        var options = sp.GetRequiredService<IOptions<LicenseOptions>>().Value;

        lic.EvaluateOnStartup();
        var status = lic.GetStatus();
        var now = DateTimeOffset.UtcNow;

        if (status.IsExpired)
        {
            store.SetReminders([]);
            _logger.LogCritical(
                "LICENSE EXPIRED: renew or activate a valid license — payment flows may remain blocked until compliance is restored (expiry snapshot: {Expiry:o}).",
                status.ExpiryDate);
            return;
        }

        var anchorDays = options.ReminderDays is { Length: > 0 }
            ? options.ReminderDays
            : new[] { 30, 15, 7, 3, 1 };

        bool onAnchorCalendarDay = anchorDays.Contains(status.DaysRemaining);
        bool inAppWindow = status.DaysRemaining <= InAppExpiryDayThreshold || onAnchorCalendarDay;

        List<LicenseReminderNotice> reminders = [];
        if (inAppWindow && status.DaysRemaining > 0)
        {
            var severity = status.DaysRemaining <= 7 ? "critical" : "warning";
            var expiryLabel = status.ExpiryDate?.ToString("yyyy-MM-dd HH:mm:ss'Z'");
            var message =
                $"License expires in {status.DaysRemaining} day(s). Trial={status.IsTrial}. ExpiryUtc={expiryLabel}.";
            reminders.Add(new LicenseReminderNotice("license_expiry_daily", severity, message, now));
        }

        store.SetReminders(reminders);

        if (status.DaysRemaining <= 7 && mail.IsSmtpConfigured)
        {
            try
            {
                var subject = $"[Regkasse] License expires in {status.DaysRemaining} day(s)";
                var body =
                    $"This is an automated license reminder from the Regkasse API host.\r\n" +
                    $"Days remaining: {status.DaysRemaining}\r\n" +
                    $"Trial mode: {status.IsTrial}\r\n" +
                    $"Expiry (UTC-based snapshot): {status.ExpiryDate?.ToString("o") ?? "n/a"}\r\n" +
                    $"Machine hash prefix: {SafePrefix(status.MachineHash)}\r\n";
                await mail.SendLicenseUrgencyEmailAsync(subject, body, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "License urgency email was not sent (SMTP error).");
            }
        }
    }

    private static TimeSpan ComputeDelayUntilUtc(int hourUtc, int minuteUtc)
    {
        hourUtc = Math.Clamp(hourUtc, 0, 23);
        minuteUtc = Math.Clamp(minuteUtc, 0, 59);

        var now = DateTime.UtcNow;
        var next = new DateTime(now.Year, now.Month, now.Day, hourUtc, minuteUtc, 0, DateTimeKind.Utc);
        if (next <= now)
            next = next.AddDays(1);

        return next - now;
    }

    private static string SafePrefix(string s) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length <= 12 ? s : s[..12]);
}
