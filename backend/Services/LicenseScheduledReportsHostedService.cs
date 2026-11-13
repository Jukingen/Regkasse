using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

/// <summary>
/// Daily UTC job: optional weekly issued-license digest and 30/15/7-day expiry alerts via SMTP
/// (<see cref="EmailSmtpOptions"/> + <see cref="LicenseReportEmailOptions"/>).
/// </summary>
public sealed class LicenseScheduledReportsHostedService : IHostedService, IDisposable
{
    private readonly IOptionsMonitor<LicenseReportEmailOptions> _reportOptions;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LicenseScheduledReportsHostedService> _logger;
    private readonly object _timerGate = new();
    private Timer? _timer;

    public LicenseScheduledReportsHostedService(
        IOptionsMonitor<LicenseReportEmailOptions> reportOptions,
        IServiceScopeFactory scopeFactory,
        ILogger<LicenseScheduledReportsHostedService> logger)
    {
        _reportOptions = reportOptions;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (OpenApiExportMode.IsEnabled)
            return Task.CompletedTask;

        ScheduleNext("host_start");
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
        var opt = _reportOptions.CurrentValue;
        var delay = ComputeDelayUntilUtc(opt.RunHourUtc, opt.RunMinuteUtc);
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
            "License scheduled reports timer armed: reason={Reason} delay={Delay} hourUtc={Hour} minuteUtc={Minute}",
            reason,
            delay,
            Math.Clamp(opt.RunHourUtc, 0, 23),
            Math.Clamp(opt.RunMinuteUtc, 0, 59));
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
                _logger.LogError(ex, "License scheduled report cycle failed.");
            }
            finally
            {
                try
                {
                    ScheduleNext("after_tick");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "License scheduled reports timer could not be rescheduled.");
                }
            }
        });
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var opt = _reportOptions.CurrentValue;
        if (!opt.EnableWeeklySummary && !opt.EnableIssuedExpiryAlerts)
            return;

        var mail = scope.ServiceProvider.GetRequiredService<ILicenseReminderEmailSender>();
        var export = scope.ServiceProvider.GetRequiredService<ILicenseExportReportService>();

        if (!mail.IsSmtpHostReadyForReports())
            return;

        var now = DateTime.UtcNow;
        var day = (int)now.DayOfWeek;

        if (opt.EnableWeeklySummary && day == Math.Clamp(opt.WeeklySummaryDayOfWeekUtc, 0, 6))
        {
            var summary = await export
                .GetSummaryAsync(new LicenseExportFilters(null, null, false, true), cancellationToken)
                .ConfigureAwait(false);
            var body =
                "Weekly license inventory summary (Regkasse API).\r\n" +
                $"Generated (UTC): {summary.GeneratedAtUtc:o}\r\n" +
                $"Issued rows (all time, no issued-date filter): {summary.IssuedTotalInDateFilter}\r\n" +
                $"Active eligible: {summary.IssuedActiveEligible}\r\n" +
                $"Revoked flag count: {summary.IssuedRevoked}\r\n" +
                $"Cancelled flag count: {summary.IssuedCancelled}\r\n" +
                $"Deleted flag count: {summary.IssuedDeleted}\r\n" +
                $"Expiring within 30 / 15 / 7 days (active): {summary.ExpiringWithin30Days} / {summary.ExpiringWithin15Days} / {summary.ExpiringWithin7Days}\r\n" +
                $"Distinct activated devices: {summary.UniqueActivatedDevices}\r\n";
            try
            {
                await mail
                    .SendLicenseReportEmailAsync("[Regkasse] Weekly license summary", body, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Weekly license summary email was not sent.");
            }
        }

        if (opt.EnableIssuedExpiryAlerts)
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await TrySendExpiryAnchorsAsync(mail, db, now, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task TrySendExpiryAnchorsAsync(
        ILicenseReminderEmailSender mail,
        AppDbContext db,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var active = await db.IssuedLicenses.AsNoTracking()
            .Where(il =>
                !il.IsDeleted
                && !il.IsCancelled
                && !il.IsRevoked
                && il.SupersededByLicenseId == null
                && il.TransferredToLicenseId == null
                && il.ExpiryAtUtc >= nowUtc)
            .Select(il => new { il.LicenseKey, il.CustomerName, il.ExpiryAtUtc })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var anchors = new[] { 30, 15, 7 };
        var lines = new List<string>();
        foreach (var row in active)
        {
            var days = (int)Math.Ceiling((row.ExpiryAtUtc - nowUtc).TotalDays);
            if (anchors.Contains(days))
            {
                var masked = LicenseExportReportService.MaskIssuedLicenseKey(row.LicenseKey);
                lines.Add($"  T-{days}d: {masked} | {row.CustomerName} | exp={row.ExpiryAtUtc:o}");
            }
        }

        if (lines.Count == 0)
            return;

        var body =
            "Issued-license expiry anchor digest (30 / 15 / 7 calendar days before expiry, UTC-based math).\r\n" +
            $"Now (UTC): {nowUtc:o}\r\n" +
            string.Join("\r\n", lines);

        try
        {
            await mail
                .SendLicenseReportEmailAsync("[Regkasse] License expiry notice (issued licenses)", body, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Issued-license expiry digest email was not sent.");
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
}
