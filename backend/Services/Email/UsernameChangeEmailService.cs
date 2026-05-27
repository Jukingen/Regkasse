using System.Net;
using System.Net.Mail;
using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Email;

public sealed class UsernameChangeEmailService : IUsernameChangeEmailService
{
    private readonly EmailSmtpOptions _options;
    private readonly ILogger<UsernameChangeEmailService> _logger;

    public UsernameChangeEmailService(IOptions<EmailSmtpOptions> options, ILogger<UsernameChangeEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.Host) && !string.IsNullOrWhiteSpace(_options.From);

    public async Task<bool> TrySendUsernameChangedAsync(
        UsernameChangedEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return false;

        var to = request.ToEmail.Trim();
        if (string.IsNullOrEmpty(to))
            return false;

        var subject = "Ihr Regkasse-Benutzername wurde geändert";
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
            _logger.LogInformation(
                "Username change notification sent to {Email} ({Old} -> {New}).",
                to,
                request.OldUsername,
                request.NewUsername);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Username change notification could not be sent to {Email} ({Old} -> {New}).",
                to,
                request.OldUsername,
                request.NewUsername);
            return false;
        }
    }

    private string BuildBody(UsernameChangedEmailRequest request)
    {
        var changedAtLocal = request.ChangedAtUtc.ToUniversalTime();
        var support = ResolveSupportContact();

        var lines = new List<string>
        {
            "Guten Tag,",
            string.Empty,
            "Ihr Anmelde-Benutzername bei Regkasse wurde von einem Administrator geändert.",
            string.Empty,
            $"Bisheriger Benutzername: {request.OldUsername}",
            $"Neuer Benutzername: {request.NewUsername}",
            $"Geändert von: {request.ChangedByAdminEmail}",
            $"Zeitpunkt (UTC): {changedAtLocal:yyyy-MM-dd HH:mm:ss} UTC",
            string.Empty,
            "Bitte verwenden Sie ab sofort den neuen Benutzernamen für die Anmeldung. Ihre E-Mail-Adresse bleibt unverändert.",
            string.Empty,
            "Support:",
            support,
            string.Empty,
            "Wenn Sie diese Änderung nicht erwarten, wenden Sie sich bitte umgehend an den Support.",
        };

        return string.Join(Environment.NewLine, lines);
    }

    private string ResolveSupportContact()
    {
        if (!string.IsNullOrWhiteSpace(_options.SupportContact))
            return _options.SupportContact.Trim();

        if (!string.IsNullOrWhiteSpace(_options.From))
            return _options.From.Trim();

        return "Ihr Regkasse-Ansprechpartner";
    }
}
