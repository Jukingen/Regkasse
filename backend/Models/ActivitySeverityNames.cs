namespace KasseAPI_Final.Models;

/// <summary>API-facing activity severity labels (Info, Warning, Error, Critical).</summary>
public static class ActivitySeverityNames
{
    public const string Info = "Info";
    public const string Warning = "Warning";
    public const string Error = "Error";
    public const string Critical = "Critical";

    private static readonly HashSet<string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        Info, Warning, Error, Critical,
    };

    public static string NormalizeOrDefault(string? value, string fallback = Info)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;
        var trimmed = value.Trim();
        foreach (var k in Known)
        {
            if (string.Equals(k, trimmed, StringComparison.OrdinalIgnoreCase))
                return k;
        }

        return fallback;
    }

    public static bool TryNormalizeFilter(string? filter, out string normalized)
    {
        normalized = Info;
        if (string.IsNullOrWhiteSpace(filter))
            return false;
        foreach (var k in Known)
        {
            if (string.Equals(k, filter.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                normalized = k;
                return true;
            }
        }

        return false;
    }
}
