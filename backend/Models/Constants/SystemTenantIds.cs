namespace KasseAPI_Final.Models.Constants;

/// <summary>
/// Well-known platform / system tenant for non-business fallbacks
/// (audit stamps, restore deployment-wide events, host-binding sentinel).
/// Wave-0 Guid retained for FK continuity; operational slug is <see cref="PlatformSlug"/>.
/// </summary>
public static class SystemTenantIds
{
    /// <summary>Platform sentinel row (Wave-0 seed Guid; unchanged for FK continuity).</summary>
    public static readonly Guid Platform = Guid.Parse("9c8f4e2b-1a3d-4f6e-8b7c-0d1e2f3a4b5c");

    /// <summary>Canonical platform slug.</summary>
    public const string PlatformSlug = "platform";

    /// <summary>
    /// Canonical platform slugs for EF Core LINQ (<c>Contains</c> translates to SQL <c>IN</c>).
    /// Values are lowercase to match stored slugs. In-memory checks stay on <see cref="IsPlatformSlug"/>
    /// (trim + ordinal ignore case); queries should compare <c>slug.Trim().ToLower()</c>.
    /// </summary>
    public static IReadOnlyCollection<string> PlatformSlugs { get; } = [PlatformSlug];

    /// <summary>True when <paramref name="tenantId"/> is the platform sentinel.</summary>
    public static bool IsPlatformTenantId(Guid tenantId) => tenantId == Platform;

    /// <summary>True for the platform sentinel slug.</summary>
    public static bool IsPlatformSlug(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return false;

        return string.Equals(slug.Trim(), PlatformSlug, StringComparison.OrdinalIgnoreCase);
    }
}
