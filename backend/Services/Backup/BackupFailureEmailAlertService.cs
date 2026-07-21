using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;
using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// SMTP ops alert for failed backups. Subject/body are German (operator mail);
/// logs remain English. Recipient list comes from <see cref="BackupOptions.FailureAlertEmailRecipients"/>
/// with fallbacks to activity/license reminder recipients.
/// </summary>
public sealed class BackupFailureEmailAlertService : IBackupFailureEmailAlertService
{
    public const int MaxErrorLength = 2000;
    public const string DefaultOpsRecipient = "admin@regkasse.at";

    private readonly IOptionsMonitor<BackupOptions> _backupOptions;
    private readonly IOptionsMonitor<EmailSmtpOptions> _smtpOptions;
    private readonly IOptionsMonitor<ActivityNotificationOptions> _activityOptions;
    private readonly ILogger<BackupFailureEmailAlertService> _logger;

    public BackupFailureEmailAlertService(
        IOptionsMonitor<BackupOptions> backupOptions,
        IOptionsMonitor<EmailSmtpOptions> smtpOptions,
        IOptionsMonitor<ActivityNotificationOptions> activityOptions,
        ILogger<BackupFailureEmailAlertService> logger)
    {
        _backupOptions = backupOptions;
        _smtpOptions = smtpOptions;
        _activityOptions = activityOptions;
        _logger = logger;
    }

    public bool IsSmtpConfigured
    {
        get
        {
            var smtp = _smtpOptions.CurrentValue;
            return !string.IsNullOrWhiteSpace(smtp.Host) && !string.IsNullOrWhiteSpace(smtp.From);
        }
    }

    public async Task SendFailureAlertAsync(
        string tenantSlug,
        string error,
        Guid? backupRunId = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsSmtpConfigured)
            return;

        var recipients = ResolveRecipients();
        if (recipients.Count == 0)
            return;

        var slug = SanitizeSlug(tenantSlug);
        var safeError = SanitizeError(error);
        var subject = BuildSubject(slug);
        var body = BuildBody(slug, safeError, backupRunId, correlationId, DateTime.UtcNow);

        var smtp = _smtpOptions.CurrentValue;
        foreach (var to in recipients)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await TrySendOneAsync(smtp, to, subject, body, backupRunId, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    internal static string BuildSubject(string tenantSlug) =>
        $"⚠️ Backup fehlgeschlagen: {tenantSlug}";

    internal static string BuildBody(
        string tenantSlug,
        string error,
        Guid? backupRunId,
        string? correlationId,
        DateTime utcNow)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Backup für Mandant {tenantSlug} ist fehlgeschlagen.");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Fehler: {error}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Zeit (UTC): {utcNow:O}");
        if (backupRunId.HasValue)
            sb.AppendLine(CultureInfo.InvariantCulture, $"Backup-Run-ID: {backupRunId.Value:D}");
        if (!string.IsNullOrWhiteSpace(correlationId))
            sb.AppendLine(CultureInfo.InvariantCulture, $"Korrelations-ID: {correlationId.Trim()}");
        sb.AppendLine();
        sb.AppendLine("Bitte überprüfen Sie das System.");
        return sb.ToString();
    }

    internal static string SanitizeSlug(string? tenantSlug)
    {
        if (string.IsNullOrWhiteSpace(tenantSlug))
            return BackupRunTenantSlugResolver.DeploymentSlug;

        var trimmed = tenantSlug.Trim();
        if (trimmed.Length > 80)
            trimmed = trimmed[..80];
        return trimmed;
    }

    internal static string SanitizeError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return "(kein Detail)";

        var cleaned = error.Trim()
            .Replace('\r', ' ')
            .Replace('\n', ' ');
        if (cleaned.Length > MaxErrorLength)
            cleaned = cleaned[..MaxErrorLength] + "…";
        return cleaned;
    }

    private IReadOnlyList<string> ResolveRecipients()
    {
        var backup = _backupOptions.CurrentValue;
        var smtp = _smtpOptions.CurrentValue;
        var activity = _activityOptions.CurrentValue;

        var fromConfig = ParseRecipients(backup.FailureAlertEmailRecipients);
        if (fromConfig.Count > 0)
            return fromConfig;

        var fallback = ParseRecipients(activity.FallbackEmailRecipients)
            .Concat(ParseRecipients(smtp.LicenseReminderRecipients))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return fallback;
    }

    private async Task TrySendOneAsync(
        EmailSmtpOptions smtp,
        string to,
        string subject,
        string body,
        Guid? backupRunId,
        CancellationToken cancellationToken)
    {
        using var msg = new MailMessage
        {
            From = new MailAddress(smtp.From!.Trim()),
            Subject = subject,
            Body = body,
            IsBodyHtml = false,
        };
        msg.To.Add(to);

#pragma warning disable CA1416
#pragma warning disable SYSLIB0014
        using var client = new SmtpClient(smtp.Host!.Trim(), smtp.Port)
        {
            EnableSsl = smtp.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
        };

        if (!string.IsNullOrWhiteSpace(smtp.User))
            client.Credentials = new NetworkCredential(smtp.User.Trim(), smtp.Password ?? string.Empty);
#pragma warning restore SYSLIB0014
#pragma warning restore CA1416

        try
        {
            await client.SendMailAsync(msg, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "Backup failure alert email sent to {EmailMasked} for run {BackupRunId}",
                MaskEmail(to),
                backupRunId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Backup failure alert email could not be sent to {EmailMasked} for run {BackupRunId}",
                MaskEmail(to),
                backupRunId);
        }
    }

    private static List<string> ParseRecipients(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];
        return raw.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(e => e.Contains('@', StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        return at <= 1 ? "***" : $"{email[..2]}***{email[at..]}";
    }
}
