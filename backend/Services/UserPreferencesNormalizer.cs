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

    public static readonly HashSet<string> AllowedTimeZones =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Europe/Vienna",
            "Europe/Berlin",
            "Europe/Zurich",
            "Europe/London",
            "Europe/Istanbul",
            "America/New_York",
            "UTC",
        };

    public static readonly HashSet<string> AllowedLanguages =
        new(StringComparer.OrdinalIgnoreCase) { "de", "en", "tr" };

    public const string DefaultDateFormat = "DD.MM.YYYY";
    public const string DefaultTimeZone = "Europe/Vienna";
    public const string DefaultLanguage = "de";

    public static string NormalizeThemeMode(string? value) =>
        value != null && AllowedThemeModes.Contains(value) ? value.ToLowerInvariant() : "system";

    public static string NormalizeDensityMode(string? value) =>
        value != null && AllowedDensityModes.Contains(value) ? value.ToLowerInvariant() : "standard";

    public static string NormalizeDefaultPage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "/dashboard";
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
        if (string.IsNullOrWhiteSpace(value))
            return DefaultDateFormat;
        var trimmed = value.Trim();
        return trimmed switch
        {
            "de-AT" or "tr-TR" => "DD.MM.YYYY",
            "en-US" => "MM/DD/YYYY",
            "auto" => DefaultDateFormat,
            _ when AllowedDateFormats.Contains(trimmed) =>
                AllowedDateFormats.First(f => f.Equals(trimmed, StringComparison.OrdinalIgnoreCase)),
            _ => DefaultDateFormat,
        };
    }

    public static string? NormalizeTimeFormat(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "24h";
        var trimmed = value.Trim();
        return AllowedTimeFormats.Contains(trimmed) ? trimmed.ToLowerInvariant() : "24h";
    }

    public static string NormalizeTimeZone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DefaultTimeZone;
        var trimmed = value.Trim();
        return AllowedTimeZones.Contains(trimmed) ? trimmed : DefaultTimeZone;
    }

    public static string NormalizeLanguage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DefaultLanguage;
        var trimmed = value.Trim().ToLowerInvariant();
        return AllowedLanguages.Contains(trimmed) ? trimmed : DefaultLanguage;
    }
}
