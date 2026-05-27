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

    /// <summary>Support line in transactional user emails (e.g. username change). Falls back to <see cref="From"/>.</summary>
    public string? SupportContact { get; set; }

    /// <summary>Recipients for license reminder escalation (comma- or semicolon-separated).</summary>
    public string? LicenseReminderRecipients { get; set; }

    /// <summary>
    /// Recipients for scheduled license inventory / export digest emails. When empty, falls back to
    /// <see cref="LicenseReminderRecipients"/>.
    /// </summary>
    public string? LicenseReportRecipients { get; set; }
}
