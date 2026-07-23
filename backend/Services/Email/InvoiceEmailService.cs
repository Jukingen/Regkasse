using System.Net;
using System.Net.Mail;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Email;

public sealed class InvoiceEmailService : IInvoiceEmailService
{
    private readonly EmailSmtpOptions _options;
    private readonly IFileNamingService _fileNaming;
    private readonly ILogger<InvoiceEmailService> _logger;

    public InvoiceEmailService(
        IOptions<EmailSmtpOptions> options,
        IFileNamingService fileNaming,
        ILogger<InvoiceEmailService> logger)
    {
        _options = options.Value;
        _fileNaming = fileNaming;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.Host) && !string.IsNullOrWhiteSpace(_options.From);

    public async Task<bool> TrySendInvoiceAsync(
        Invoice invoice,
        byte[] pdfContent,
        string recipientEmail,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Invoice email skipped: SMTP not configured.");
            return false;
        }

        var to = recipientEmail.Trim();
        if (string.IsNullOrEmpty(to))
            return false;

        var subject = $"Ihre Rechnung #{invoice.InvoiceNumber}";
        var body = BuildEmailBody(invoice);
        var fileName = _fileNaming.GenerateFileName(
            InvoiceExportFileNames.PdfPrefix,
            "pdf",
            registerId: string.IsNullOrWhiteSpace(invoice.KassenId) ? null : invoice.KassenId,
            additional: invoice.InvoiceNumber,
            tenantSlug: invoice.Tenant?.Slug);

        using var msg = new MailMessage
        {
            From = new MailAddress(_options.From!.Trim()),
            Subject = subject,
            Body = body,
            IsBodyHtml = true,
        };
        msg.To.Add(to);

        await using var stream = new MemoryStream(pdfContent);
        var attachment = new Attachment(stream, fileName, "application/pdf");
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

        try
        {
            await client.SendMailAsync(msg, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Invoice {InvoiceNumber} emailed to {Recipient}.", invoice.InvoiceNumber, to);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invoice {InvoiceNumber} could not be emailed to {Recipient}.", invoice.InvoiceNumber, to);
            return false;
        }
#pragma warning restore SYSLIB0014
#pragma warning restore CA1416
    }

    private static string BuildEmailBody(Invoice invoice)
    {
        return $"""
            <h2>Sehr geehrte/r Kunde/in,</h2>
            <p>Anbei erhalten Sie Ihre Rechnung {invoice.InvoiceNumber} vom {invoice.InvoiceDate:dd.MM.yyyy}.</p>
            <p>Rechnungsbetrag: <strong>{invoice.TotalAmount:N2} EUR</strong></p>
            <br/>
            <p>Mit freundlichen Grüßen,<br/>Ihr Regkasse Team</p>
            """;
    }
}
