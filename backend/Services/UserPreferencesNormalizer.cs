namespace KasseAPI_Final.Services;

/// <summary>Validates and normalizes FA user preference payloads.</summary>
public static class UserPreferencesNormalizer
{
    public static readonly HashSet<string> AllowedThemeModes =
        new(StringComparer.OrdinalIgnoreCase) { "light", "dark", "system" };

    public static readonly HashSet<string> AllowedDensityModes =
        new(StringComparer.OrdinalIgnoreCase) { "comfortable", "standard", "compact" };

    public static readonly HashSet<string> AllowedDefaultPages =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "/dashboard",
            "/admin/users",
            "/kassenverwaltung",
            "/reporting",
            // Legacy values normalized on read
            "/users",
            "/receipts",
            "/settings",
        };

    public static readonly HashSet<string> AllowedDateFormats =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "DD.MM.YYYY",
            "YYYY-MM-DD",
            "MM/DD/YYYY",
            // Legacy locale presets
            "de-AT",
            "en-US",
            "tr-TR",
        };

    public static readonly HashSet<string> AllowedTimeFormats =
        new(StringComparer.OrdinalIgnoreCase) { "24h", "12h" };

    public static string NormalizeThemeMode(string? value) =>
        value != null && AllowedThemeModes.Contains(value) ? value.ToLowerInvariant() : "system";

    public static string NormalizeDensityMode(string? value) =>
        value != null && AllowedDensityModes.Contains(value) ? value.ToLowerInvariant() : "standard";

    public static string NormalizeDefaultPage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "/dashboard";
        var trimmed = value.Trim();
        return trimmed switch
        {
            "/users" => "/admin/users",
            "/receipts" or "/settings" => "/dashboard",
            _ when AllowedDefaultPages.Contains(trimmed) => trimmed,
            _ => "/dashboard",
        };
    }

    public static string? NormalizeDateFormat(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "DD.MM.YYYY";
        var trimmed = value.Trim();
        return trimmed switch
        {
            "de-AT" or "tr-TR" => "DD.MM.YYYY",
            "en-US" => "MM/DD/YYYY",
            "auto" => "DD.MM.YYYY",
            _ when AllowedDateFormats.Contains(trimmed) => trimmed,
            _ => trimmed.Length <= 20 ? trimmed : trimmed[..20],
        };
    }

    public static string? NormalizeTimeFormat(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return AllowedTimeFormats.Contains(trimmed) ? trimmed.ToLowerInvariant() : null;
    }
}
