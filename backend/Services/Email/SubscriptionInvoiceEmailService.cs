using System.Globalization;
using System.Net;
using System.Net.Mail;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.Billing;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Email;

public sealed class SubscriptionInvoiceEmailService : ISubscriptionInvoiceEmailService
{
    private readonly EmailSmtpOptions _options;
    private readonly ILogger<SubscriptionInvoiceEmailService> _logger;

    public SubscriptionInvoiceEmailService(
        IOptions<EmailSmtpOptions> options,
        ILogger<SubscriptionInvoiceEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.Host) && !string.IsNullOrWhiteSpace(_options.From);

    public Task<bool> TrySendIssuedAsync(
        SubscriptionInvoiceDto invoice,
        byte[] pdfContent,
        string? tenantEmail,
        CancellationToken cancellationToken = default)
    {
        var period = FormatPeriod(invoice);
        var subject = $"Ihre Rechnung {invoice.InvoiceNumber}";
        var body = $"""
            <h2>Sehr geehrte Damen und Herren,</h2>
            <p>anbei erhalten Sie Ihre Abonnement-Rechnung <strong>{invoice.InvoiceNumber}</strong>
            für den Zeitraum {period}.</p>
            <p>Rechnungsbetrag: <strong>{FormatMoney(invoice.AmountGross, invoice.Currency)}</strong></p>
            <p>Mit freundlichen Grüßen,<br/>Ihr Regkasse Team</p>
            """;
        return TrySendAsync(invoice, pdfContent, tenantEmail, subject, body, cancellationToken);
    }

    public Task<bool> TrySendPaidConfirmationAsync(
        SubscriptionInvoiceDto invoice,
        byte[] pdfContent,
        string? tenantEmail,
        CancellationToken cancellationToken = default)
    {
        var subject = $"Zahlungseingang bestätigt — {invoice.InvoiceNumber}";
        var body = $"""
            <h2>Sehr geehrte Damen und Herren,</h2>
            <p>wir bestätigen den Zahlungseingang für Rechnung <strong>{invoice.InvoiceNumber}</strong>
            über <strong>{FormatMoney(invoice.AmountGross, invoice.Currency)}</strong>.</p>
            <p>Mit freundlichen Grüßen,<br/>Ihr Regkasse Team</p>
            """;
        return TrySendAsync(invoice, pdfContent, tenantEmail, subject, body, cancellationToken);
    }

    public Task<bool> TrySendReminderAsync(
        SubscriptionInvoiceDto invoice,
        byte[] pdfContent,
        string? tenantEmail,
        int daysOverdue,
        CancellationToken cancellationToken = default)
    {
        var days = Math.Max(0, daysOverdue);
        var subject = $"Zahlungserinnerung — Rechnung {invoice.InvoiceNumber}";
        var body = $"""
            <h2>Sehr geehrte Damen und Herren,</h2>
            <p>die Rechnung <strong>{invoice.InvoiceNumber}</strong> ist seit {days} Tag(en) offen.
            Offener Betrag: <strong>{FormatMoney(invoice.AmountGross, invoice.Currency)}</strong>.</p>
            <p>Bitte überweisen Sie den Betrag unter Angabe der Rechnungsnummer.</p>
            <p>Mit freundlichen Grüßen,<br/>Ihr Regkasse Team</p>
            """;
        return TrySendAsync(invoice, pdfContent, tenantEmail, subject, body, cancellationToken);
    }

    private async Task<bool> TrySendAsync(
        SubscriptionInvoiceDto invoice,
        byte[] pdfContent,
        string? tenantEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning(
                "Subscription invoice email skipped: SMTP not configured ({InvoiceNumber}).",
                invoice.InvoiceNumber);
            return false;
        }

        var to = tenantEmail?.Trim();
        if (string.IsNullOrEmpty(to))
        {
            _logger.LogWarning(
                "Subscription invoice email skipped: tenant has no email ({InvoiceNumber}).",
                invoice.InvoiceNumber);
            return false;
        }

        using var msg = new MailMessage
        {
            From = new MailAddress(_options.From!.Trim()),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
        };
        msg.To.Add(to);

        await using var stream = new MemoryStream(pdfContent);
        var attachment = new Attachment(stream, $"{invoice.InvoiceNumber}.pdf", "application/pdf");
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
            _logger.LogInformation(
                "Subscription invoice {InvoiceNumber} emailed to {Recipient}.",
                invoice.InvoiceNumber,
                to);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Subscription invoice {InvoiceNumber} could not be emailed to {Recipient}.",
                invoice.InvoiceNumber,
                to);
            return false;
        }
#pragma warning restore SYSLIB0014
#pragma warning restore CA1416
    }

    private static string FormatPeriod(SubscriptionInvoiceDto invoice) =>
        $"{invoice.PeriodStartUtc:yyyy-MM-dd} – {invoice.PeriodEndUtc.AddDays(-1):yyyy-MM-dd} UTC";

    private static string FormatMoney(decimal amount, string currency) =>
        $"{amount.ToString("0.00", CultureInfo.InvariantCulture)} {currency}";
}
