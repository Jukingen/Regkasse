namespace KasseAPI_Final.Tenancy;

/// <summary>
/// Maps legacy dev/test tenant slugs to canonical <see cref="DemoTenantAdminSeed"/> slugs.
/// Keeps POS presets and docs compatible without duplicate tenant rows.
/// </summary>
public static class DevTenantSlugAliases
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["test_cafe"] = "cafe",
        ["test-cafe"] = "cafe",
        ["test_bar"] = "bar",
        ["test-bar"] = "bar",
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
