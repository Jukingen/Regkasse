using System.Net;
using System.Net.Mail;
using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.DataDeletion;

public interface IDataDeletionNotificationSender
{
    Task SendAsync(
        IReadOnlyList<string> to,
        IReadOnlyList<string> cc,
        string subject,
        string plainBody,
        CancellationToken ct = default);
}

/// <summary>Best-effort SMTP notifier for deletion request / confirmation (skipped when SMTP unset).</summary>
public sealed class DataDeletionNotificationSender : IDataDeletionNotificationSender
{
    private readonly IOptions<EmailSmtpOptions> _options;
    private readonly ILogger<DataDeletionNotificationSender> _logger;

    public DataDeletionNotificationSender(
        IOptions<EmailSmtpOptions> options,
        ILogger<DataDeletionNotificationSender> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task SendAsync(
        IReadOnlyList<string> to,
        IReadOnlyList<string> cc,
        string subject,
        string plainBody,
        CancellationToken ct = default)
    {
        var opt = _options.Value;
        var host = opt.Host?.Trim();
        var from = opt.From?.Trim();
        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(from))
        {
            _logger.LogInformation("Data deletion email skipped (SMTP not configured). Subject={Subject}", subject);
            return;
        }

        if (to.Count == 0 && cc.Count == 0)
        {
            _logger.LogInformation("Data deletion email skipped (no recipients). Subject={Subject}", subject);
            return;
        }

        using var msg = new MailMessage
        {
            From = new MailAddress(from),
            Subject = subject,
            Body = plainBody,
            IsBodyHtml = false,
        };

        foreach (var r in to.Distinct(StringComparer.OrdinalIgnoreCase))
            msg.To.Add(r);
        foreach (var r in cc.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (msg.To.Any(t => string.Equals(t.Address, r, StringComparison.OrdinalIgnoreCase)))
                continue;
            msg.CC.Add(r);
        }

        if (msg.To.Count == 0 && msg.CC.Count > 0)
        {
            msg.To.Add(msg.CC[0]);
            msg.CC.RemoveAt(0);
        }

#pragma warning disable CA1416
#pragma warning disable SYSLIB0014
        using var client = new SmtpClient(host, opt.Port)
        {
            EnableSsl = opt.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
        };
        if (!string.IsNullOrWhiteSpace(opt.User))
            client.Credentials = new NetworkCredential(opt.User.Trim(), opt.Password ?? string.Empty);
#pragma warning restore SYSLIB0014
#pragma warning restore CA1416

        await client.SendMailAsync(msg, ct).ConfigureAwait(false);
        _logger.LogInformation(
            "Data deletion email sent. To={ToCount}, Cc={CcCount}, Subject={Subject}",
            msg.To.Count,
            msg.CC.Count,
            subject);
    }
}
