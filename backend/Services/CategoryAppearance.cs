using System.Text.RegularExpressions;

namespace KasseAPI_Final.Services;

/// <summary>
/// Shared category icon/color normalization for admin CRUD, catalog, and seed backfill.
/// Icons are emoji (FA/POS); legacy Ionicons glyph names are mapped to emoji.
/// </summary>
public static class CategoryAppearance
{
    public const string DefaultIcon = "📦";

    private static readonly Regex HexColorRegex = new(
        @"^#(?:[0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Dictionary<string, string> LegacyIonIconToEmoji =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["wine"] = "🍷",
            ["restaurant"] = "🍽️",
            ["ice-cream"] = "🍰",
            ["fast-food"] = "🍔",
            ["cafe"] = "☕",
            ["folder"] = DefaultIcon,
            ["nutrition"] = "🥗",
            ["pizza"] = "🍕",
            ["grid"] = DefaultIcon,
        };

    public static string ResolveIcon(string? icon)
    {
        var normalized = NormalizeIcon(icon);
        return string.IsNullOrEmpty(normalized) ? DefaultIcon : normalized;
    }

    public static string? NormalizeIcon(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon))
            return null;

        var trimmed = icon.Trim();
        if (LegacyIonIconToEmoji.TryGetValue(trimmed, out var emoji))
            return emoji;

        return trimmed.Length <= 50 ? trimmed : trimmed[..50];
    }

    public static bool TryNormalizeColor(string? color, out string? normalized, out string? error)
    {
        normalized = null;
        error = null;

        if (string.IsNullOrWhiteSpace(color))
            return true;

        var trimmed = color.Trim();
        if (!HexColorRegex.IsMatch(trimmed))
        {
            error = "Color must be a hex value such as #E53935.";
            return false;
        }

        if (trimmed.Length == 4)
        {
            normalized =
                $"#{char.ToUpperInvariant(trimmed[1])}{char.ToUpperInvariant(trimmed[1])}" +
                $"{char.ToUpperInvariant(trimmed[2])}{char.ToUpperInvariant(trimmed[2])}" +
                $"{char.ToUpperInvariant(trimmed[3])}{char.ToUpperInvariant(trimmed[3])}";
            return true;
        }

        normalized = $"#{trimmed[1..].ToUpperInvariant()}";
        return true;
    }
}
