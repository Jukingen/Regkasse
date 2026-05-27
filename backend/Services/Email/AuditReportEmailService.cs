using System.Net;
using System.Net.Mail;
using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Email;

public sealed class AuditReportEmailService : IAuditReportEmailService
{
    private readonly EmailSmtpOptions _options;
    private readonly ILogger<AuditReportEmailService> _logger;

    public AuditReportEmailService(IOptions<EmailSmtpOptions> options, ILogger<AuditReportEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.Host) && !string.IsNullOrWhiteSpace(_options.From);

    public async Task SendReportAsync(
        IReadOnlyList<string> recipients,
        string subject,
        string plainBody,
        string attachmentFileName,
        byte[] attachmentContent,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || recipients.Count == 0)
        {
            _logger.LogWarning("Audit report email skipped: SMTP or recipients not configured.");
            return;
        }

        using var msg = new MailMessage
        {
            From = new MailAddress(_options.From!.Trim()),
            Subject = subject,
            Body = plainBody,
            IsBodyHtml = false,
        };

        foreach (var r in recipients)
            msg.To.Add(r);

        await using var stream = new MemoryStream(attachmentContent);
        var attachment = new Attachment(stream, attachmentFileName, contentType);
        msg.Attachments.Add(attachment);

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

        await client.SendMailAsync(msg, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Audit report email sent to {Count} recipients: {Subject}", recipients.Count, subject);
    }
}
