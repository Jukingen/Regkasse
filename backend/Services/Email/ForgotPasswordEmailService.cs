using System.Net;
using System.Net.Mail;
using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Email;

public sealed class ForgotPasswordEmailService : IForgotPasswordEmailService
{
    private readonly EmailSmtpOptions _options;
    private readonly ILogger<ForgotPasswordEmailService> _logger;

    public ForgotPasswordEmailService(IOptions<EmailSmtpOptions> options, ILogger<ForgotPasswordEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.Host) && !string.IsNullOrWhiteSpace(_options.From);

    public async Task<bool> TrySendForgotPasswordAsync(
        ForgotPasswordEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(request.ResetToken))
            return false;

        var to = request.ToEmail.Trim();
        if (string.IsNullOrEmpty(to))
            return false;

        using var msg = new MailMessage
        {
            From = new MailAddress(_options.From!.Trim()),
            Subject = "Regkasse Admin – Passwort zurücksetzen",
            Body = BuildBody(request),
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
            _logger.LogInformation("Forgot-password email sent to {EmailMasked}.", MaskEmail(to));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Forgot-password email could not be sent to {EmailMasked}.", MaskEmail(to));
            return false;
        }
    }

    private static string BuildBody(ForgotPasswordEmailRequest request)
    {
        return string.Join(
            Environment.NewLine,
            [
                "Guten Tag,",
                string.Empty,
                "Sie haben angefordert, Ihr Passwort für Regkasse Admin zurückzusetzen.",
                string.Empty,
                "Verwenden Sie den folgenden Code in der Passwort-Zurücksetzen-Oberfläche:",
                string.Empty,
                request.ResetToken,
                string.Empty,
                "Wenn Sie diese Anfrage nicht gestellt haben, ignorieren Sie diese E-Mail.",
            ]);
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        return at <= 1 ? "***" : $"{email[..2]}***{email[at..]}";
    }
}
