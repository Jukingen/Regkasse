using System.Net;
using System.Net.Mail;
using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Email;

public sealed class ManualRestoreApprovalEmailService : IManualRestoreApprovalEmailService
{
    private readonly EmailSmtpOptions _options;
    private readonly ILogger<ManualRestoreApprovalEmailService> _logger;

    public ManualRestoreApprovalEmailService(IOptions<EmailSmtpOptions> options, ILogger<ManualRestoreApprovalEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.Host) && !string.IsNullOrWhiteSpace(_options.From);

    public async Task<int> TrySendApprovalRequestsAsync(
        ManualRestoreApprovalEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || request.ApproverEmails.Count == 0)
            return 0;

        var sent = 0;
        foreach (var raw in request.ApproverEmails.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var to = raw.Trim();
            if (string.IsNullOrEmpty(to))
                continue;

            if (await TrySendOneAsync(request, to, cancellationToken).ConfigureAwait(false))
                sent++;
        }

        return sent;
    }

    private async Task<bool> TrySendOneAsync(
        ManualRestoreApprovalEmailRequest request,
        string to,
        CancellationToken cancellationToken)
    {
        using var msg = new MailMessage
        {
            From = new MailAddress(_options.From!.Trim()),
            Subject = "[Regkasse] Manual restore approval required",
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
            _logger.LogInformation(
                "Manual restore approval email sent to {EmailMasked} for request {RequestId}.",
                MaskEmail(to),
                request.RequestId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Manual restore approval email could not be sent to {EmailMasked} for request {RequestId}.",
                MaskEmail(to),
                request.RequestId);
            return false;
        }
    }

    private static string BuildBody(ManualRestoreApprovalEmailRequest request)
    {
        var lines = new List<string>
        {
            "A Super Admin requested a validation-only manual restore.",
            string.Empty,
            $"Request ID: {request.RequestId}",
            $"Requested by: {request.RequestedByEmail}",
            $"Backup run ID: {request.BackupRunId}",
            $"Target database (isolated): {request.TargetDatabaseName}",
            $"Token expires (UTC): {request.ExpiresAtUtc:O}",
            string.Empty,
        };

        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            lines.Add("Reason:");
            lines.Add(request.Reason.Trim());
            lines.Add(string.Empty);
        }

        lines.AddRange(
        [
            "Your 6-digit approval code (enter via POST /api/admin/restore/approve/{requestId}):",
            request.ApprovalToken,
            string.Empty,
            "This operation never restores into the production application database.",
            "If you did not expect this request, reject it and investigate.",
        ]);

        return string.Join(Environment.NewLine, lines);
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        return at <= 1 ? "***" : $"{email[..2]}***{email[at..]}";
    }
}
