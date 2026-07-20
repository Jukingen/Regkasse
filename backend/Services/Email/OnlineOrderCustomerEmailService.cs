using System.Globalization;
using System.Net;
using System.Net.Mail;
using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Email;

/// <summary>Sends German customer emails for online orders when <c>Email:Smtp</c> is configured.</summary>
public sealed class OnlineOrderCustomerEmailService : IOnlineOrderCustomerEmailService
{
    private readonly EmailSmtpOptions _options;
    private readonly ILogger<OnlineOrderCustomerEmailService> _logger;

    public OnlineOrderCustomerEmailService(
        IOptions<EmailSmtpOptions> options,
        ILogger<OnlineOrderCustomerEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.Host) && !string.IsNullOrWhiteSpace(_options.From);

    public Task<bool> TrySendOrderConfirmationAsync(
        OnlineOrderCustomerEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        var subject = $"Bestellbestätigung – {request.OrderNumber}";
        var body = string.Join(
            Environment.NewLine,
            [
                $"Hallo {request.CustomerName},",
                string.Empty,
                $"Ihre Bestellung {request.OrderNumber} wurde bestätigt.",
                $"Gesamtbetrag: {FormatMoney(request.Total, request.Currency)}",
                $"Art: {request.OrderType}",
                $"Geschätzte Wartezeit: ca. {request.EstimatedMinutes} Minuten",
                string.Empty,
                "Vielen Dank für Ihre Bestellung!",
            ]);

        return TrySendAsync(request.ToEmail, subject, body, request.OrderNumber, cancellationToken);
    }

    public Task<bool> TrySendOrderStatusAsync(
        OnlineOrderCustomerEmailRequest request,
        string statusHeadline,
        string statusBody,
        CancellationToken cancellationToken = default)
    {
        var subject = $"{statusHeadline} – {request.OrderNumber}";
        var body = string.Join(
            Environment.NewLine,
            [
                $"Hallo {request.CustomerName},",
                string.Empty,
                statusBody,
                $"Gesamtbetrag: {FormatMoney(request.Total, request.Currency)}",
                string.Empty,
                "Vielen Dank!",
            ]);

        return TrySendAsync(request.ToEmail, subject, body, request.OrderNumber, cancellationToken);
    }

    private async Task<bool> TrySendAsync(
        string toEmail,
        string subject,
        string body,
        string orderNumber,
        CancellationToken cancellationToken)
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
            Body = body,
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
            _logger.LogInformation(
                "Online order email sent to {Email} for {OrderNumber}.",
                to,
                orderNumber);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Online order email could not be sent to {Email} for {OrderNumber}.",
                to,
                orderNumber);
            return false;
        }
    }

    private static string FormatMoney(decimal amount, string currency) =>
        string.Create(
            CultureInfo.GetCultureInfo("de-AT"),
            $"{amount:0.00} {currency}");
}
