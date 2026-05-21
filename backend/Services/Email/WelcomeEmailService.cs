using System.Net;
using System.Net.Mail;
using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Email;

public sealed class WelcomeEmailService : IWelcomeEmailService
{
    private readonly EmailSmtpOptions _options;
    private readonly ILogger<WelcomeEmailService> _logger;

    public WelcomeEmailService(IOptions<EmailSmtpOptions> options, ILogger<WelcomeEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.Host) && !string.IsNullOrWhiteSpace(_options.From);

    public async Task<bool> TrySendWelcomeAsync(
        WelcomeEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return false;

        var to = request.ToEmail.Trim();
        if (string.IsNullOrEmpty(to))
            return false;

        var subject = $"Willkommen bei Regkasse – {request.TenantName}";
        var body = BuildBody(request);

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
            _logger.LogInformation("Welcome email sent to {Email} for tenant slug {Slug}.", to, request.TenantSlug);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Welcome email could not be sent to {Email} for tenant slug {Slug}.", to, request.TenantSlug);
            return false;
        }
    }

    private static string BuildBody(WelcomeEmailRequest request)
    {
        var changeLine = request.ForcePasswordChangeOnNextLogin
            ? "Bitte ändern Sie Ihr Passwort nach der ersten Anmeldung."
            : string.Empty;

        var lines = new List<string>
        {
            "Willkommen bei Regkasse!",
            string.Empty,
            $"Ihr Mandant \"{request.TenantName}\" wurde eingerichtet.",
            string.Empty,
            "Zugangsdaten:",
            $"Anmelde-E-Mail: {request.AdminEmail}",
            $"Passwort: {request.TemporaryPassword}",
            string.Empty,
        };

        if (!string.IsNullOrEmpty(changeLine))
        {
            lines.Add(changeLine);
            lines.Add(string.Empty);
        }

        lines.AddRange(
        [
            "Erste Schritte:",
            $"1. Anmelden unter: {request.PortalUrl}",
            "2. Passwort ändern",
            "3. Produkte anpassen",
            "4. Kasse mit Drucker verbinden",
            "5. Test-Transaktion durchführen",
            string.Empty,
            "Bei Fragen wenden Sie sich an Ihren Regkasse-Ansprechpartner.",
        ]);

        return string.Join(Environment.NewLine, lines);
    }
}
