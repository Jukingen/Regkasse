using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Email;

/// <summary>Generic SMTP email sender for Super Admin / platform notifications.</summary>
public interface IEmailService
{
    bool IsConfigured { get; }

    Task<bool> TrySendHtmlAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One SMTP message with a plain-text body and an HTML alternate view.
    /// Logs the recipient count, not the addresses.
    /// </summary>
    Task<bool> TrySendHtmlAndPlainAsync(
        IReadOnlyCollection<string> toEmails,
        string subject,
        string htmlBody,
        string plainBody,
        CancellationToken cancellationToken = default);
}

public sealed class EmailService : IEmailService
{
    private readonly EmailSmtpOptions _options;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSmtpOptions> options, ILogger<EmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.Host) && !string.IsNullOrWhiteSpace(_options.From);

    public async Task<bool> TrySendHtmlAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return false;

        var to = toEmail.Trim();
        if (string.IsNullOrEmpty(to) || string.IsNullOrWhiteSpace(subject))
            return false;

        using var msg = new MailMessage
        {
            From = new MailAddress(_options.From!.Trim()),
            Subject = subject.Trim(),
            Body = htmlBody ?? string.Empty,
            IsBodyHtml = true,
        };
        msg.To.Add(to);

#pragma warning disable CA1416
#pragma warning disable SYSLIB0014
        using var client = new SmtpClient(_options.Host!.Trim(), _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
        };

        if (!string.IsNullOrWhiteSpace(_options.User))
            client.Credentials = new NetworkCredential(_options.User.Trim(), _options.Password ?? string.Empty);
#pragma warning restore SYSLIB0014
#pragma warning restore CA1416

        try
        {
            await client.SendMailAsync(msg, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Email sent to {Email} subject {Subject}", to, subject);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Email could not be sent to {Email} subject {Subject}", to, subject);
            return false;
        }
    }

    public async Task<bool> TrySendHtmlAndPlainAsync(
        IReadOnlyCollection<string> toEmails,
        string subject,
        string htmlBody,
        string plainBody,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(subject))
            return false;

        var addresses = new List<MailAddress>();
        foreach (var raw in toEmails)
        {
            if (MailAddress.TryCreate(raw, out var address)
                && addresses.TrueForAll(existing => !string.Equals(existing.Address, address.Address, StringComparison.OrdinalIgnoreCase)))
                addresses.Add(address);
        }

        if (addresses.Count == 0)
            return false;

        using var msg = new MailMessage
        {
            From = new MailAddress(_options.From!.Trim()),
            Subject = subject.Trim(),
            Body = plainBody ?? string.Empty,
            IsBodyHtml = false,
        };
        foreach (var address in addresses)
            msg.To.Add(address);

        msg.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            plainBody ?? string.Empty,
            Encoding.UTF8,
            MediaTypeNames.Text.Plain));
        msg.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            htmlBody ?? string.Empty,
            Encoding.UTF8,
            MediaTypeNames.Text.Html));

        if (!await SendAsync(msg, cancellationToken).ConfigureAwait(false))
            return false;

        _logger.LogInformation(
            "Email sent to {RecipientCount} recipient(s) subject {Subject}",
            addresses.Count,
            subject);
        return true;
    }

    private async Task<bool> SendAsync(MailMessage msg, CancellationToken cancellationToken)
    {
#pragma warning disable CA1416
#pragma warning disable SYSLIB0014
        using var client = new SmtpClient(_options.Host!.Trim(), _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
        };

        if (!string.IsNullOrWhiteSpace(_options.User))
            client.Credentials = new NetworkCredential(_options.User.Trim(), _options.Password ?? string.Empty);
#pragma warning restore SYSLIB0014
#pragma warning restore CA1416

        try
        {
            await client.SendMailAsync(msg, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Email could not be sent subject {Subject}", msg.Subject);
            return false;
        }
    }
}
