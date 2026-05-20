using System.Net;
using System.Net.Mail;
using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.AdminTenants;

public sealed class TenantInvitationEmailSender : ITenantInvitationEmailSender
{
    private readonly EmailSmtpOptions _options;
    private readonly ILogger<TenantInvitationEmailSender> _logger;

    public TenantInvitationEmailSender(IOptions<EmailSmtpOptions> options, ILogger<TenantInvitationEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.Host) && !string.IsNullOrWhiteSpace(_options.From);

    public async Task<bool> TrySendInvitationAsync(
        string toEmail,
        string subject,
        string plainBody,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return false;

        var to = toEmail.Trim();
        if (string.IsNullOrEmpty(to))
            return false;

        using var msg = new MailMessage
        {
            From = new MailAddress(_options.From!.Trim()),
            Subject = subject,
            Body = plainBody,
            IsBodyHtml = false,
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
            _logger.LogInformation("Tenant invitation email sent to {Email}.", to);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tenant invitation email could not be sent to {Email}.", to);
            return false;
        }
    }
}
