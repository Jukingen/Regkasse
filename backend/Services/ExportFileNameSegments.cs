using System.Text;

namespace KasseAPI_Final.Services;

/// <summary>Shared filesystem-safe segments for download / attachment file names.</summary>
public static class ExportFileNameSegments
{
    public static string LocalStamp(DateTime? at = null) =>
        (at ?? DateTime.Now).ToString("yyyyMMdd_HHmmss");

    public static string DateOnly(DateTime? value, string fallback = "all") =>
        value.HasValue ? value.Value.ToString("yyyyMMdd") : fallback;

    /// <summary>
    /// Keeps ASCII letters, digits, hyphen, underscore; maps whitespace/path separators to underscore.
    /// </summary>
    public static string Sanitize(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var sb = new StringBuilder(value.Length);
        foreach (var c in value.Trim())
        {
            if (char.IsAsciiLetterOrDigit(c) || c is '-' or '_')
                sb.Append(c);
            else if (char.IsWhiteSpace(c) || c is '.' or '/' or '\\' or ':')
                sb.Append('_');
        }

        var sanitized = sb.ToString().Trim('_');
        return string.IsNullOrEmpty(sanitized) ? fallback : sanitized;
    }
}
