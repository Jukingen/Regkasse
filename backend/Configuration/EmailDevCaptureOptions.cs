namespace KasseAPI_Final.Configuration;

/// <summary>
/// Development-only capture of outbound transactional emails to disk (no SMTP required).
/// </summary>
public sealed class EmailDevCaptureOptions
{
    public const string SectionName = "Email:DevCapture";

    /// <summary>When true in Development, emails are written under <see cref="Directory"/>.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Relative to content root. Default: App_Data/dev-mail</summary>
    public string Directory { get; set; } = "App_Data/dev-mail";
}
