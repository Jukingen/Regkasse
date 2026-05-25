using System.Net;
using System.Net.Mail;
using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

public interface ILicenseReminderEmailSender
{
    bool IsSmtpConfigured { get; }

    Task SendLicenseUrgencyEmailAsync(string subject, string plainBody, CancellationToken cancellationToken = default);

    Task<bool> TrySendTenantLicenseReminderAsync(
        string toEmail,
        string subject,
        string plainBody,
        CancellationToken cancellationToken = default);

    /// <summary>Sends to <see cref="EmailSmtpOptions.LicenseReportRecipients"/> or reminder recipients when SMTP is ready.</summary>
    Task SendLicenseReportEmailAsync(string subject, string plainBody, CancellationToken cancellationToken = default);

    /// <summary>Host/from configured and at least one report or reminder recipient is parseable.</summary>
    bool IsSmtpHostReadyForReports();
}

/// <summary>SMTP-based escalation for license urgency (skipped when Host is unset).</summary>
public sealed class LicenseReminderEmailSender : ILicenseReminderEmailSender
{
    private readonly IOptions<EmailSmtpOptions> _options;
    private readonly ILogger<LicenseReminderEmailSender> _logger;

    public LicenseReminderEmailSender(IOptions<EmailSmtpOptions> options, ILogger<LicenseReminderEmailSender> logger)
    {
        _options = options;
        _logger = logger;
    }

    public bool IsSmtpConfigured
    {
        get
        {
            if (!IsSmtpHostReady())
                return false;
            var to = ParseRecipients(_options.Value.LicenseReminderRecipients);
            return to.Count > 0;
        }
    }

    private bool IsSmtpHostReady()
    {
        var h = _options.Value.Host?.Trim();
        var from = _options.Value.From?.Trim();
        return !string.IsNullOrEmpty(h) && !string.IsNullOrEmpty(from);
    }

    /// <inheritdoc />
    public bool IsSmtpHostReadyForReports() =>
        IsSmtpHostReady() && ParseRecipients(ResolveReportRecipientsRaw()).Count > 0;

    private string? ResolveReportRecipientsRaw()
    {
        var report = _options.Value.LicenseReportRecipients?.Trim();
        if (!string.IsNullOrEmpty(report))
            return report;
        return _options.Value.LicenseReminderRecipients;
    }

    public async Task SendLicenseUrgencyEmailAsync(string subject, string plainBody, CancellationToken cancellationToken = default)
    {
        var opt = _options.Value;
        if (!IsSmtpConfigured || !IsSmtpHostReady())
            return;

        var recipients = ParseRecipients(opt.LicenseReminderRecipients);
        if (recipients.Count == 0)
            return;

        using var msg = new MailMessage
        {
            From = new MailAddress(opt.From!.Trim()),
            Subject = subject,
            Body = plainBody,
            IsBodyHtml = false,
        };

        foreach (var r in recipients)
            msg.To.Add(r);

#pragma warning disable CA1416 // SmtpClient supported on server targets; MailKit not yet introduced.
#pragma warning disable SYSLIB0014 // SmtpClient obsoletion — acceptable for optional admin SMTP.
        using var client = new SmtpClient(opt.Host!.Trim(), opt.Port)
        {
            EnableSsl = opt.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
        };

        if (!string.IsNullOrWhiteSpace(opt.User))
            client.Credentials = new NetworkCredential(opt.User!.Trim(), opt.Password ?? string.Empty);
#pragma warning restore SYSLIB0014
#pragma warning restore CA1416

        try
        {
            await client.SendMailAsync(msg, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("License reminder email sent to {Count} recipient(s).", recipients.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "License reminder email could not be sent.");
            throw;
        }
    }

    public async Task<bool> TrySendTenantLicenseReminderAsync(
        string toEmail,
        string subject,
        string plainBody,
        CancellationToken cancellationToken = default)
    {
        var opt = _options.Value;
        if (!IsSmtpHostReady())
            return false;

        var recipient = toEmail.Trim();
        if (recipient.Length == 0)
            return false;

        using var msg = new MailMessage
        {
            From = new MailAddress(opt.From!.Trim()),
            Subject = subject,
            Body = plainBody,
            IsBodyHtml = false,
        };
        msg.To.Add(recipient);

#pragma warning disable CA1416
#pragma warning disable SYSLIB0014
        using var client = new SmtpClient(opt.Host!.Trim(), opt.Port)
        {
            EnableSsl = opt.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
        };

        if (!string.IsNullOrWhiteSpace(opt.User))
            client.Credentials = new NetworkCredential(opt.User!.Trim(), opt.Password ?? string.Empty);
#pragma warning restore SYSLIB0014
#pragma warning restore CA1416

        try
        {
            await client.SendMailAsync(msg, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Tenant license reminder email sent to {Email}.", recipient);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tenant license reminder email could not be sent to {Email}.", recipient);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task SendLicenseReportEmailAsync(string subject, string plainBody, CancellationToken cancellationToken = default)
    {
        var opt = _options.Value;
        var h = opt.Host?.Trim();
        var from = opt.From?.Trim();
        var recipients = ParseRecipients(ResolveReportRecipientsRaw());
        if (!IsSmtpHostReady() || recipients.Count == 0)
            return;

        using var msg = new MailMessage
        {
            From = new MailAddress(from!),
            Subject = subject,
            Body = plainBody,
            IsBodyHtml = false,
        };

        foreach (var r in recipients)
            msg.To.Add(r);

#pragma warning disable CA1416
#pragma warning disable SYSLIB0014
        using var client = new SmtpClient(h!, opt.Port)
        {
            EnableSsl = opt.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
        };

        if (!string.IsNullOrWhiteSpace(opt.User))
            client.Credentials = new NetworkCredential(opt.User!.Trim(), opt.Password ?? string.Empty);
#pragma warning restore SYSLIB0014
#pragma warning restore CA1416

        try
        {
            await client.SendMailAsync(msg, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("License report email sent to {Count} recipient(s).", recipients.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "License report email could not be sent.");
            throw;
        }
    }

    private static List<MailAddress> ParseRecipients(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        var separators = new[] { ',', ';', '\t', '\n', '\r' };
        var parts = raw.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var list = new List<MailAddress>();
        foreach (var p in parts)
        {
            var t = p.Trim();
            if (t.Length == 0) continue;
            try
            {
                list.Add(new MailAddress(t));
            }
            catch
            {
                /* skip invalid token */
            }
        }

        return list;
    }
}
