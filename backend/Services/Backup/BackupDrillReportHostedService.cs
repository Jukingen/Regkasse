using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Weekly Backup/DR drill summary. Proof time comes from
/// <see cref="IBackupRecoverabilitySummaryService"/> (the recoverability-summary source).
/// Empty recipients are logged at Information and write <see cref="AuditEventType.BackupDrillReportSkipped"/>.
/// </summary>
public sealed class BackupDrillReportHostedService : IHostedService, IDisposable
{
    private readonly IOptionsMonitor<BackupDrillReportOptions> _options;
    private readonly IOptionsMonitor<RestoreVerificationOptions> _restoreOptions;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _time;
    private readonly ILogger<BackupDrillReportHostedService> _logger;
    private readonly object _timerGate = new();
    private Timer? _timer;

    public BackupDrillReportHostedService(
        IOptionsMonitor<BackupDrillReportOptions> options,
        IOptionsMonitor<RestoreVerificationOptions> restoreOptions,
        IServiceScopeFactory scopeFactory,
        TimeProvider time,
        ILogger<BackupDrillReportHostedService> logger)
    {
        _options = options;
        _restoreOptions = restoreOptions;
        _scopeFactory = scopeFactory;
        _time = time;
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

    /// <summary>One schedule check (test hook via InternalsVisibleTo). Does not send when disabled or off-day.</summary>
    internal async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        var opt = _options.CurrentValue;
        if (!opt.Enabled)
            return;

        var now = _time.GetUtcNow().UtcDateTime;
        if ((int)now.DayOfWeek != Math.Clamp(opt.DayOfWeekUtc, 0, 6))
            return;

        if (ParseRecipients(opt.Recipients).Count == 0)
        {
            _logger.LogInformation("Backup drill weekly summary skipped: no recipients configured.");
            using var skipScope = _scopeFactory.CreateScope();
            var correlationId = Guid.NewGuid().ToString("N");
            await skipScope.ServiceProvider.GetRequiredService<IAuditLogService>()
                .LogSystemOperationAsync(
                    action: "BACKUP_DRILL_REPORT_SKIPPED",
                    entityType: "BackupDrillReport",
                    userId: "system",
                    userRole: "System",
                    description: "Weekly backup drill summary skipped.",
                    correlationIdOverride: correlationId,
                    actionType: AuditEventType.BackupDrillReportSkipped,
                    newValues: new { Reason = "no-recipients", CorrelationId = correlationId })
                .ConfigureAwait(false);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        try
        {
            var body = await BuildAsync(scope.ServiceProvider, now, cancellationToken).ConfigureAwait(false);
            var mail = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var sent = await mail.TrySendHtmlAndPlainAsync(
                    ParseRecipients(opt.Recipients),
                    "[Regkasse] Weekly backup drill summary",
                    body.Html,
                    body.Plain,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!sent)
            {
                await WriteAuditAsync(scope.ServiceProvider, success: false, body.AuditValues)
                    .ConfigureAwait(false);
                return;
            }

            await WriteAuditAsync(scope.ServiceProvider, success: true, body.AuditValues)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Backup drill weekly summary was not sent ({ExceptionType}).",
                ex.GetType().Name);
            await WriteAuditAsync(scope.ServiceProvider, success: false, new { Error = ex.GetType().Name })
                .ConfigureAwait(false);
        }
    }

    private async Task<ReportBody> BuildAsync(
        IServiceProvider services,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var summary = await services
            .GetRequiredService<IBackupRecoverabilitySummaryService>()
            .GetAsync(accessScope: null, cancellationToken)
            .ConfigureAwait(false);

        var daysSince = summary.RestoreProofAgeSeconds is long seconds
            ? (int)(seconds / 86400L)
            : (int?)null;
        var proofAt = summary.LastSuccessfulRestoreProofAt;
        var restore = _restoreOptions.CurrentValue;
        DateTime? nextDrill = null;
        if (restore.ScheduledWeeklyDrillEnabled)
        {
            var cadenceDays = Math.Max(1, restore.ScheduledProofCadenceDays);
            nextDrill = proofAt?.AddDays(cadenceDays) ?? nowUtc;
        }

        var db = services.GetRequiredService<AppDbContext>();
        var rate = await DrillSuccessRateLast30DaysAsync(db, nowUtc, cancellationToken).ConfigureAwait(false);
        var acceptances = await ReadAcceptancesAsync(db, cancellationToken).ConfigureAwait(false);

        var plain = new StringBuilder()
            .AppendLine("Weekly backup drill summary (Regkasse API).")
            .Append("lastSuccessfulRestoreProofAt: ").AppendLine(FormatInstant(proofAt))
            .Append("lastSuccessfulRestoreDrillAtUtc: ").AppendLine(FormatInstant(proofAt))
            .Append("daysSinceLastProof: ").AppendLine(daysSince?.ToString() ?? "n/a")
            .Append("nextScheduledDrillUtc: ").AppendLine(FormatInstant(nextDrill))
            .Append("drillSuccessRateLast30Days: ").AppendLine(rate?.ToString("0.0") ?? "n/a")
            .Append("knownGapAcceptances: ").AppendLine(acceptances.Plain)
            .ToString();

        var html = new StringBuilder()
            .Append("<p>Weekly backup drill summary (Regkasse API).</p><ul>")
            .Append("<li>lastSuccessfulRestoreProofAt: ").Append(Encode(FormatInstant(proofAt))).Append("</li>")
            .Append("<li>lastSuccessfulRestoreDrillAtUtc: ").Append(Encode(FormatInstant(proofAt))).Append("</li>")
            .Append("<li>daysSinceLastProof: ").Append(Encode(daysSince?.ToString() ?? "n/a")).Append("</li>")
            .Append("<li>nextScheduledDrillUtc: ").Append(Encode(FormatInstant(nextDrill))).Append("</li>")
            .Append("<li>drillSuccessRateLast30Days: ").Append(Encode(rate?.ToString("0.0") ?? "n/a")).Append("</li>")
            .Append("<li>knownGapAcceptances: ").Append(acceptances.Html).Append("</li>")
            .Append("</ul>")
            .ToString();

        return new ReportBody(
            html,
            plain,
            new
            {
                LastSuccessfulRestoreProofAt = proofAt,
                LastSuccessfulRestoreDrillAtUtc = proofAt,
                DaysSinceLastProof = daysSince,
                NextScheduledDrillUtc = nextDrill,
                DrillSuccessRateLast30Days = rate,
                KnownGapAcceptances = acceptances.Count,
            });
    }

    private static async Task<double?> DrillSuccessRateLast30DaysAsync(
        AppDbContext db,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var since = nowUtc.AddDays(-30);
        var statuses = await db.RestoreVerificationRuns.AsNoTracking()
            .Where(run => run.RequestedAt >= since
                          && (run.Status == RestoreVerificationStatus.Succeeded
                              || run.Status == RestoreVerificationStatus.Failed))
            .Select(run => run.Status)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (statuses.Count == 0)
            return null;

        var succeeded = statuses.Count(status => status == RestoreVerificationStatus.Succeeded);
        return Math.Round(100.0 * succeeded / statuses.Count, 1);
    }

    private static async Task<AcceptanceText> ReadAcceptancesAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var rows = await db.TenantSettings.AsNoTracking()
            .Where(row => row.Key == BackupKnownGapAcceptanceService.SettingsKey && row.TenantId != null)
            .Select(row => new { row.TenantId, row.Value })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (rows.Count == 0)
            return new AcceptanceText("none", "none", 0);

        var plain = new StringBuilder();
        var html = new StringBuilder();
        foreach (var row in rows.OrderBy(row => row.TenantId))
        {
            var parsed = ParseAcceptance(row.Value);
            plain.Append(row.TenantId).Append(' ').Append(parsed).Append("; ");
            html.Append(Encode(row.TenantId!.Value.ToString("D"))).Append(' ').Append(Encode(parsed)).Append("<br/>");
        }

        return new AcceptanceText(plain.ToString().Trim(), html.ToString(), rows.Count);
    }

    private static string ParseAcceptance(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return "[]";

        try
        {
            using var document = JsonDocument.Parse(json);
            var gaps = document.RootElement.TryGetProperty("acceptedGaps", out var list) && list.ValueKind == JsonValueKind.Array
                ? string.Join(",", list.EnumerateArray().Select(item => item.GetString()).Where(id => !string.IsNullOrWhiteSpace(id)))
                : string.Empty;
            var at = document.RootElement.TryGetProperty("acceptedAtUtc", out var stamp) && stamp.ValueKind == JsonValueKind.String
                ? stamp.GetString()
                : null;
            return string.IsNullOrEmpty(at) ? $"[{gaps}]" : $"[{gaps}] at {at}";
        }
        catch (JsonException)
        {
            return "[unreadable]";
        }
    }

    private static async Task WriteAuditAsync(
        IServiceProvider services,
        bool success,
        object newValues)
    {
        var audit = services.GetRequiredService<IAuditLogService>();
        await audit.LogSystemOperationAsync(
                action: success ? "BACKUP_DRILL_REPORT_SENT" : "BACKUP_DRILL_REPORT_FAILED",
                entityType: "BackupDrillReport",
                userId: "system",
                userRole: "System",
                description: success
                    ? "Weekly backup drill summary sent."
                    : "Weekly backup drill summary was not sent.",
                actionType: success
                    ? AuditEventType.BackupDrillReportSent
                    : AuditEventType.BackupDrillReportFailed,
                newValues: newValues)
            .ConfigureAwait(false);
    }

    private void ScheduleNext(string reason)
    {
        var opt = _options.CurrentValue;
        var delay = ComputeDelayUntilUtc(_time, opt.RunHourUtc, opt.RunMinuteUtc);
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
            "Backup drill report timer armed: reason={Reason} delay={Delay}",
            reason,
            delay);
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
                _logger.LogError(ex, "Backup drill report cycle failed.");
            }
            finally
            {
                try
                {
                    ScheduleNext("after_tick");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Backup drill report timer could not be rescheduled.");
                }
            }
        });
    }

    private static TimeSpan ComputeDelayUntilUtc(TimeProvider time, int hourUtc, int minuteUtc)
    {
        hourUtc = Math.Clamp(hourUtc, 0, 23);
        minuteUtc = Math.Clamp(minuteUtc, 0, 59);
        var now = time.GetUtcNow().UtcDateTime;
        var next = new DateTime(now.Year, now.Month, now.Day, hourUtc, minuteUtc, 0, DateTimeKind.Utc);
        if (next <= now)
            next = next.AddDays(1);
        return next - now;
    }

    internal static List<string> ParseRecipients(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        var parts = raw.Split([',', ';', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var list = new List<string>();
        foreach (var part in parts)
        {
            if (MailAddress.TryCreate(part, out var address)
                && list.TrueForAll(existing => !string.Equals(existing, address.Address, StringComparison.OrdinalIgnoreCase)))
                list.Add(address.Address);
        }

        return list;
    }

    private static string FormatInstant(DateTime? value) =>
        value.HasValue ? value.Value.ToUniversalTime().ToString("o") : "n/a";

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private sealed record ReportBody(string Html, string Plain, object AuditValues);

    private sealed record AcceptanceText(string Plain, string Html, int Count);
}
