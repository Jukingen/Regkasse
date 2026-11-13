using System.Net;
using System.Net.Mail;
using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Email;

public interface IExportEmailSmtpService
{
    bool IsConfigured { get; }

    Task SendAsync(
        IReadOnlyList<string> recipients,
        string subject,
        string plainBody,
        string? attachmentFileName,
        byte[]? attachmentContent,
        string? contentType,
        CancellationToken cancellationToken = default);
}

/// <summary>SMTP sender for export emails (optional single attachment).</summary>
public sealed class ExportEmailSmtpService : IExportEmailSmtpService
{
    private readonly EmailSmtpOptions _options;
    private readonly ILogger<ExportEmailSmtpService> _logger;

    public ExportEmailSmtpService(IOptions<EmailSmtpOptions> options, ILogger<ExportEmailSmtpService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.Host) && !string.IsNullOrWhiteSpace(_options.From);

    public async Task SendAsync(
        IReadOnlyList<string> recipients,
        string subject,
        string plainBody,
        string? attachmentFileName,
        byte[]? attachmentContent,
        string? contentType,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || recipients.Count == 0)
        {
            _logger.LogWarning("Export email skipped: SMTP or recipients not configured.");
            throw new InvalidOperationException("SMTP is not configured.");
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

        MemoryStream? stream = null;
        if (attachmentContent is { Length: > 0 } && !string.IsNullOrWhiteSpace(attachmentFileName))
        {
            stream = new MemoryStream(attachmentContent);
            msg.Attachments.Add(
                new Attachment(
                    stream,
                    attachmentFileName,
                    string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType));
        }

        try
        {
#pragma warning disable CA1416
#pragma warning disable SYSLIB0014
            using var client = new SmtpClient(_options.Host!.Trim(), _options.Port)
            {
                EnableSsl = _options.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
            };
#pragma warning restore SYSLIB0014
#pragma warning restore CA1416

            if (!string.IsNullOrWhiteSpace(_options.User))
                client.Credentials = new NetworkCredential(_options.User.Trim(), _options.Password ?? string.Empty);

            await client.SendMailAsync(msg, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "Export email sent to {Count} recipients: {Subject} (attachment={HasAttachment})",
                recipients.Count,
                subject,
                attachmentContent is { Length: > 0 });
        }
        finally
        {
            if (stream is not null)
                await stream.DisposeAsync().ConfigureAwait(false);
        }
    }
}
