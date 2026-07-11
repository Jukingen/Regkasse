namespace KasseAPI_Final.Tenancy;

/// <summary>
/// Normalizes dev tenant slugs for API resolution. Legacy cafe/bar/test_* aliases map to dev/prod presets.
/// </summary>
public static class DevTenantSlugAliases
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["test_cafe"] = "dev",
        ["test-cafe"] = "dev",
        ["cafe"] = "dev",
        ["test_bar"] = "prod",
        ["test-bar"] = "prod",
        ["bar"] = "prod",
    };

    /// <summary>Returns the canonical slug when <paramref name="slug"/> is a known alias; otherwise trimmed input.</summary>
    public static string ResolveCanonical(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return slug;
        }

        var trimmed = slug.Trim();
        return Aliases.TryGetValue(trimmed, out var canonical) ? canonical : trimmed;
    }
}
