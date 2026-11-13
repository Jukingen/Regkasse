using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Email;

public sealed class DevCapturingForgotPasswordEmailService : IForgotPasswordEmailService
{
    private readonly ForgotPasswordEmailService _smtpService;
    private readonly DevEmailOutboxWriter _outbox;
    private readonly EmailDevCaptureOptions _devCapture;
    private readonly IWebHostEnvironment _environment;

    public DevCapturingForgotPasswordEmailService(
        ForgotPasswordEmailService smtpService,
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

    public async Task<bool> TrySendForgotPasswordAsync(
        ForgotPasswordEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ResetToken))
            return false;

        var to = request.ToEmail.Trim();
        if (string.IsNullOrEmpty(to))
            return false;

        var subject = ForgotPasswordEmailService.Subject;
        var body = ForgotPasswordEmailService.BuildBody(request);
        var sent = false;

        if (DevCaptureActive)
        {
            var path = await _outbox.TryWriteAsync(to, subject, body, cancellationToken).ConfigureAwait(false);
            sent = path != null;
        }

        if (_smtpService.IsConfigured)
            sent = await _smtpService.TrySendForgotPasswordAsync(request, cancellationToken).ConfigureAwait(false) || sent;

        return sent;
    }
}
