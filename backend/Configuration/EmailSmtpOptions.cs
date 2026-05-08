namespace KasseAPI_Final.Configuration;

/// <summary>
/// SMTP client settings for transactional notifications (e.g. license urgency mail).
/// If <see cref="Host"/> is empty, email sending is skipped.
/// </summary>
public sealed class EmailSmtpOptions
{
    public const string SectionName = "Email:Smtp";

    public string? Host { get; set; }

    public int Port { get; set; } = 587;

    public bool EnableSsl { get; set; } = true;

    public string? User { get; set; }

    public string? Password { get; set; }

    /// <summary>Envelope-from address.</summary>
    public string? From { get; set; }

    /// <summary>Recipients for license reminder escalation (comma- or semicolon-separated).</summary>
    public string? LicenseReminderRecipients { get; set; }
}
