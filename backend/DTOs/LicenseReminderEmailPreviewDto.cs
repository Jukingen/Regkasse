namespace KasseAPI_Final.DTOs;

/// <summary>Sample mandant license reminder email for Super Admin FA preview (no SMTP send).</summary>
public sealed record LicenseReminderEmailPreviewDto(
    string Subject,
    string HtmlBody,
    string PlainBody,
    int DaysUntilExpiry,
    DateTime SampleExpiryDate);
