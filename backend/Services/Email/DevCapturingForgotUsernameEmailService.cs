using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Email;

/// <summary>
/// Development wrapper: captures email to disk and optionally still sends via SMTP.
/// </summary>
public sealed class DevCapturingForgotUsernameEmailService : IForgotUsernameEmailService
{
    private readonly ForgotUsernameEmailService _smtpService;
    private readonly DevEmailOutboxWriter _outbox;
    private readonly EmailDevCaptureOptions _devCapture;
    private readonly IWebHostEnvironment _environment;

    public DevCapturingForgotUsernameEmailService(
        ForgotUsernameEmailService smtpService,
        DevEmailOutboxWriter outbox,
        IOptions<EmailDevCaptureOptions> devCapture,
        IWebHostEnvironment environment)
    {
        _smtpService = smtpService;
        _outbox = outbox;
        _devCapture = devCapture.Value;
        _environment = environment;
    }

    public bool IsConfigured =>
        DevCaptureActive || _smtpService.IsConfigured;

    private bool DevCaptureActive =>
        _environment.IsDevelopment() && _devCapture.Enabled;

    public async Task<bool> TrySendForgotUsernameAsync(
        ForgotUsernameEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Usernames.Count == 0)
            return false;

        var to = request.ToEmail.Trim();
        if (string.IsNullOrEmpty(to))
            return false;

        var subject = ForgotUsernameEmailService.Subject;
        var body = ForgotUsernameEmailService.BuildBody(request);
        if (DevCaptureActive && !string.IsNullOrWhiteSpace(request.DevAccountSummary))
        {
            body = string.Join(
                Environment.NewLine,
                body,
                string.Empty,
                "--- Dev debug ---",
                request.DevAccountSummary.Trim());
        }
        var sent = false;

        if (DevCaptureActive)
        {
            var path = await _outbox.TryWriteAsync(to, subject, body, cancellationToken).ConfigureAwait(false);
            sent = path != null;
        }

        if (_smtpService.IsConfigured)
            sent = await _smtpService.TrySendForgotUsernameAsync(request, cancellationToken).ConfigureAwait(false) || sent;

        return sent;
    }
}
