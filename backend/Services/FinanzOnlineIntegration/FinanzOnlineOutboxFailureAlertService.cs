using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

public interface IFinanzOnlineOutboxOpsAlert
{
    /// <summary>Returns true when the alert was delivered, or when no Slack/email channel is configured.</summary>
    Task<bool> SendAsync(int failedLastHour, CancellationToken cancellationToken = default);
}

/// <summary>
/// Ops alert for a FinanzOnline outbox failure spike.
/// Email uses <see cref="ActivityNotificationOptions.FallbackEmailRecipients"/> and SMTP.
/// Slack uses <see cref="ActivityNotificationOptions.WebhookUrl"/> when <see cref="ActivityNotificationOptions.WebhookEnabled"/> is true.
/// Set <see cref="ActivityNotificationOptions.WebhookFormat"/> to <c>Slack</c> for an incoming-webhook <c>text</c> payload.
/// </summary>
public sealed class FinanzOnlineOutboxOpsAlert : IFinanzOnlineOutboxOpsAlert
{
    private readonly IEmailService _email;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<ActivityNotificationOptions> _options;
    private readonly ILogger<FinanzOnlineOutboxOpsAlert> _logger;

    public FinanzOnlineOutboxOpsAlert(
        IEmailService email,
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<ActivityNotificationOptions> options,
        ILogger<FinanzOnlineOutboxOpsAlert> logger)
    {
        _email = email;
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<bool> SendAsync(int failedLastHour, CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        var subject = BuildSubject(failedLastHour);
        var body = BuildBody(failedLastHour, DateTime.UtcNow);
        var emailed = await TryEmailAsync(options, subject, body, cancellationToken).ConfigureAwait(false);
        var posted = await TryWebhookAsync(options, body, failedLastHour, cancellationToken).ConfigureAwait(false);

        var emailConfigured = _email.IsConfigured && ParseRecipients(options.FallbackEmailRecipients).Count > 0;
        var webhookConfigured = options.WebhookEnabled && !string.IsNullOrWhiteSpace(options.WebhookUrl);
        if (!emailConfigured && !webhookConfigured)
        {
            _logger.LogWarning(
                "FinanzOnline outbox alert has no Slack webhook or email recipients. Failed in the last hour: {FailedCount}.",
                failedLastHour);
            return true;
        }

        return emailed || posted;
    }

    internal static string BuildSubject(int failedLastHour) =>
        $"FinanzOnline outbox: {failedLastHour.ToString(CultureInfo.InvariantCulture)} failed submissions in the last hour";

    internal static string BuildBody(int failedLastHour, DateTime utcNow)
    {
        var count = failedLastHour.ToString(CultureInfo.InvariantCulture);
        var threshold = FinanzOnlineOutboxMonitoringService.HourlyFailureAlertThreshold
            .ToString(CultureInfo.InvariantCulture);
        return $"FinanzOnline outbox failed submissions in the last hour: {count}. Threshold is more than {threshold}. Time (UTC): {utcNow:O}.";
    }

    private async Task<bool> TryEmailAsync(
        ActivityNotificationOptions options,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        if (!_email.IsConfigured)
            return false;

        var recipients = ParseRecipients(options.FallbackEmailRecipients);
        if (recipients.Count == 0)
            return false;

        var html = "<p>" + WebUtility.HtmlEncode(body) + "</p>";
        return await _email.TrySendHtmlAndPlainAsync(recipients, subject, html, body, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> TryWebhookAsync(
        ActivityNotificationOptions options,
        string body,
        int failedLastHour,
        CancellationToken cancellationToken)
    {
        if (!options.WebhookEnabled || string.IsNullOrWhiteSpace(options.WebhookUrl))
            return false;

        var timeoutSec = Math.Clamp(options.WebhookTimeoutSeconds, 1, 120);
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));
            var client = _httpClientFactory.CreateClient(ActivityEventWebhookNotifier.HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, options.WebhookUrl.Trim());
            if (!string.IsNullOrWhiteSpace(options.WebhookSecretHeaderName))
            {
                request.Headers.TryAddWithoutValidation(
                    options.WebhookSecretHeaderName.Trim(),
                    options.WebhookSecretHeaderValue ?? string.Empty);
            }

            var json = JsonSerializer.Serialize(BuildWebhookPayload(options.WebhookFormat, body, failedLastHour));
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(request, cts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "FinanzOnline outbox Slack/webhook alert returned {StatusCode}.",
                    (int)response.StatusCode);
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "FinanzOnline outbox Slack/webhook alert failed.");
            return false;
        }
    }

    internal static object BuildWebhookPayload(string? format, string text, int failedLastHour)
    {
        var normalized = format?.Trim() ?? string.Empty;
        if (normalized.Equals("Slack", StringComparison.OrdinalIgnoreCase))
            return new { text };
        if (normalized.Equals("Discord", StringComparison.OrdinalIgnoreCase))
            return new { content = text };
        return new
        {
            text,
            source = "regkasse-finanzonline-outbox",
            failedLastHour,
        };
    }

    internal static List<string> ParseRecipients(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];
        return raw.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(e => e.Contains('@', StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

public interface IFinanzOnlineOutboxFailureMonitor
{
    /// <summary>Sends at most one Slack/email alert per UTC hour when failures in the last hour exceed 5.</summary>
    Task<bool> CheckAndAlertAsync(CancellationToken cancellationToken = default);
}

public sealed class FinanzOnlineOutboxFailureMonitor : IFinanzOnlineOutboxFailureMonitor
{
    private readonly AppDbContext _db;
    private readonly IFinanzOnlineOutboxMonitoringService _monitoring;
    private readonly IFinanzOnlineOutboxOpsAlert _alert;
    private readonly IActivityEventService _activity;
    private readonly TimeProvider _time;
    private readonly ILogger<FinanzOnlineOutboxFailureMonitor> _logger;

    public FinanzOnlineOutboxFailureMonitor(
        AppDbContext db,
        IFinanzOnlineOutboxMonitoringService monitoring,
        IFinanzOnlineOutboxOpsAlert alert,
        IActivityEventService activity,
        TimeProvider time,
        ILogger<FinanzOnlineOutboxFailureMonitor> logger)
    {
        _db = db;
        _monitoring = monitoring;
        _alert = alert;
        _activity = activity;
        _time = time;
        _logger = logger;
    }

    public async Task<bool> CheckAndAlertAsync(CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var count = await _monitoring
            .CountFailedSinceAsync(tenantId: null, now.AddHours(-1), cancellationToken)
            .ConfigureAwait(false);
        if (count <= FinanzOnlineOutboxMonitoringService.HourlyFailureAlertThreshold)
            return false;

        var dedupKey = "fon-outbox-failures:" + now.ToString("yyyyMMddHH", CultureInfo.InvariantCulture);
        var alreadySent = await _db.ActivityEvents
            .IgnoreQueryFilters()
            .AnyAsync(
                e => e.TenantId == SystemTenantIds.Platform && e.DedupKey == dedupKey,
                cancellationToken)
            .ConfigureAwait(false);
        if (alreadySent)
            return false;

        var delivered = await _alert.SendAsync(count, cancellationToken).ConfigureAwait(false);
        if (!delivered)
            return false;

        await _activity.PublishAsync(
            new ActivityEventPublishRequest(
                SystemTenantIds.Platform,
                ActivityEventType.FinanzOnlineOutboxFailureSpike,
                "FinanzOnline outbox failures",
                FinanzOnlineOutboxOpsAlert.BuildBody(count, now),
                Severity: ActivitySeverityNames.Error,
                DedupKey: dedupKey,
                EntityType: "finanz_online_outbox",
                SkipOutboundDelivery: true),
            cancellationToken).ConfigureAwait(false);

        _logger.LogWarning(
            "FinanzOnline outbox alert sent. Failed in the last hour: {FailedCount}.",
            count);
        return true;
    }
}
