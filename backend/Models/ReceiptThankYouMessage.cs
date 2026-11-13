namespace KasseAPI_Final.Models;

/// <summary>
/// Tenant receipt footer (Dankesnachricht). Stored on <see cref="CompanySettings.ThankYouMessage"/>;
/// empty/null uses <see cref="Default"/>. Company description is a separate receipt line.
/// </summary>
public static class ReceiptThankYouMessage
{
    public const string Default = "Vielen Dank für Ihren Einkauf!";
    public const int MaxLength = 500;

    /// <summary>Trim and treat whitespace-only as unset (use default).</summary>
    public static string? NormalizeStored(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    public static string Resolve(CompanySettings? settings)
    {
        var custom = NormalizeStored(settings?.ThankYouMessage);
        return custom ?? Default;
    }
}
