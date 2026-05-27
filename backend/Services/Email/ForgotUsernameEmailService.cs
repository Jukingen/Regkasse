using System.Net;
using System.Net.Mail;
using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Email;

public sealed class ForgotUsernameEmailService : IForgotUsernameEmailService
{
    private readonly EmailSmtpOptions _options;
    private readonly ILogger<ForgotUsernameEmailService> _logger;

    public ForgotUsernameEmailService(IOptions<EmailSmtpOptions> options, ILogger<ForgotUsernameEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.Host) && !string.IsNullOrWhiteSpace(_options.From);

    public async Task<bool> TrySendForgotUsernameAsync(
        ForgotUsernameEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || request.Usernames.Count == 0)
            return false;

        var to = request.ToEmail.Trim();
        if (string.IsNullOrEmpty(to))
            return false;

        using var msg = new MailMessage
        {
            From = new MailAddress(_options.From!.Trim()),
            Subject = "Ihre Regkasse-Anmeldenamen",
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
            _logger.LogInformation("Forgot-username email sent to {EmailMasked}.", MaskEmail(to));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Forgot-username email could not be sent to {EmailMasked}.", MaskEmail(to));
            return false;
        }
    }

    private static string BuildBody(ForgotUsernameEmailRequest request)
    {
        var lines = new List<string>
        {
            "Guten Tag,",
            string.Empty,
            "Sie haben angefordert, Ihre Anmeldenamen für Regkasse Admin abzurufen.",
            string.Empty,
            "Folgende Benutzernamen sind mit Ihrer E-Mail-Adresse verknüpft:",
            string.Empty,
        };

        foreach (var name in request.Usernames)
            lines.Add($"  • {name}");

        lines.AddRange(
        [
            string.Empty,
            "Sie können sich mit einem dieser Benutzernamen (oder Ihrer E-Mail-Adresse) anmelden.",
            "Passwörter werden aus Sicherheitsgründen nicht per E-Mail versendet.",
            string.Empty,
            "Wenn Sie diese Anfrage nicht gestellt haben, ignorieren Sie diese E-Mail.",
        ]);

        return string.Join(Environment.NewLine, lines);
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        return at <= 1 ? "***" : $"{email[..2]}***{email[at..]}";
    }
}
