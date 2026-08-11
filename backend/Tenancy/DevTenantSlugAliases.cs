namespace KasseAPI_Final.Tenancy;

/// <summary>
/// Normalizes dev tenant slugs for API resolution. Legacy cafe/bar/test_* aliases map to dev/prod presets.
/// Unused legacy <c>default</c> maps to seeded <c>dev</c> (development DX; avoid resolving the Wave-0 row).
/// </summary>
public static class DevTenantSlugAliases
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        // Default tenant is excluded as it's not used in development — prefer seeded `dev`.
        ["default"] = "dev",
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
