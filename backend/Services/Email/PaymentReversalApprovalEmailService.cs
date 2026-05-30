using System.Net;
using System.Net.Mail;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Email;

public sealed class PaymentReversalApprovalEmailService : IPaymentReversalApprovalEmailService
{
    private readonly EmailSmtpOptions _options;
    private readonly ILogger<PaymentReversalApprovalEmailService> _logger;

    public PaymentReversalApprovalEmailService(
        IOptions<EmailSmtpOptions> options,
        ILogger<PaymentReversalApprovalEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> TrySendApprovalTokenAsync(
        IReadOnlyList<string> approverEmails,
        string approvalToken,
        PaymentDetails payment,
        PaymentReversalOperation operation,
        decimal? refundAmount,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Host) || string.IsNullOrWhiteSpace(_options.From))
            return 0;

        var amount = operation == PaymentReversalOperation.Refund
            ? refundAmount ?? payment.TotalAmount
            : payment.TotalAmount;

        var sent = 0;
        foreach (var raw in approverEmails.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var to = raw.Trim();
            if (string.IsNullOrEmpty(to))
                continue;

            try
            {
                using var msg = new MailMessage
                {
                    From = new MailAddress(_options.From!.Trim()),
                    Subject = "[Regkasse] Payment reversal approval required",
                    Body =
                        $"A high-risk payment {operation.ToString().ToLowerInvariant()} requires manager approval.\n\n" +
                        $"Payment ID: {payment.Id}\n" +
                        $"Receipt: {payment.ReceiptNumber}\n" +
                        $"Amount: {amount:N2} EUR\n" +
                        $"Approval token (valid until {expiresAtUtc:u} UTC): {approvalToken}\n",
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

                await client.SendMailAsync(msg, cancellationToken).ConfigureAwait(false);
                sent++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send payment reversal approval email to {Email}", to);
            }
        }

        return sent;
    }
}
