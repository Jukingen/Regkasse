using System.Net;
using System.Net.Mail;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Activity;

public interface IActivityEventEmailNotifier
{
    Task TrySendAsync(
        ActivityEvent evt,
        NotificationConfig tenantConfig,
        CancellationToken cancellationToken = default);
}

/// <summary>SMTP delivery for warning/critical activity events when configured.</summary>
public sealed class ActivityEventEmailNotifier : IActivityEventEmailNotifier
{
    private readonly IOptions<EmailSmtpOptions> _smtp;
    private readonly IOptions<ActivityNotificationOptions> _activity;
    private readonly ILogger<ActivityEventEmailNotifier> _logger;

    public ActivityEventEmailNotifier(
        IOptions<EmailSmtpOptions> smtp,
        IOptions<ActivityNotificationOptions> activity,
        ILogger<ActivityEventEmailNotifier> logger)
    {
        _smtp = smtp;
        _activity = activity;
        _logger = logger;
    }

    public Task TrySendAsync(
        ActivityEvent evt,
        NotificationConfig tenantConfig,
        CancellationToken cancellationToken = default)
    {
        var forceCritical = NotificationConfigEvaluator.RequiresImmediateCriticalEmail(evt.Type);
        if (!forceCritical
            && !NotificationConfigEvaluator.ShouldDeliverEmail(tenantConfig, evt.Type, evt.Severity))
            return Task.CompletedTask;

        if (forceCritical
            && !NotificationConfigEvaluator.IsEventTypeEnabled(tenantConfig, evt.Type))
            return Task.CompletedTask;

        var smtp = _smtp.Value;
        var host = smtp.Host?.Trim();
        var from = smtp.From?.Trim();
        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(from))
            return Task.CompletedTask;

        var recipients = tenantConfig.EmailRecipients
            .Where(e => e.Contains('@', StringComparison.Ordinal))
            .ToList();

        if (recipients.Count == 0)
        {
            var activityOpt = _activity.Value;
            recipients = ParseRecipients(activityOpt.FallbackEmailRecipients)
                .Concat(ParseRecipients(smtp.LicenseReminderRecipients))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (recipients.Count == 0)
            return Task.CompletedTask;

        var subject = $"[Regkasse] {evt.Title}";
        var body = string.IsNullOrWhiteSpace(evt.Description)
            ? $"{evt.Type} ({evt.Severity}) at {evt.CreatedAtUtc:u}"
            : $"{evt.Description}\n\nType: {evt.Type}\nSeverity: {evt.Severity}\nTime (UTC): {evt.CreatedAtUtc:u}";

        try
        {
            using var msg = new MailMessage
            {
                From = new MailAddress(from),
                Subject = subject,
                Body = body,
                IsBodyHtml = false,
            };
            foreach (var to in recipients)
                msg.To.Add(to);

            using var client = new SmtpClient(host, smtp.Port)
            {
                EnableSsl = smtp.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
            };
            if (!string.IsNullOrWhiteSpace(smtp.User))
                client.Credentials = new NetworkCredential(smtp.User, smtp.Password);

            client.Send(msg);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Activity event email failed EventId={EventId} Type={Type}", evt.Id, evt.Type);
        }

        return Task.CompletedTask;
    }

    private static List<string> ParseRecipients(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];
        return raw.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(e => e.Contains('@', StringComparison.Ordinal))
            .ToList();
    }
}
