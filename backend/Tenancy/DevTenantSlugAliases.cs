namespace KasseAPI_Final.Tenancy;

/// <summary>
/// Normalizes legacy demo tenant slugs for API resolution (cafe/bar aliases → dev/prod presets).
/// Callers must apply this only when <c>IHostEnvironment.IsDevelopment()</c> —
/// Production/Staging must resolve exact slugs (no cafe→dev remapping).
/// Platform sentinel is not aliased here — Production admin host binds to <c>platform</c> directly.
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
