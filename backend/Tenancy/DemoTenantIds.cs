namespace KasseAPI_Final.Tenancy;

/// <summary>
/// Stable ids for local demo tenants (dev / prod). Must match migration
/// <c>SeedDemoTenantAdmins</c>, <c>ReplaceDemoCafeBarWithProd</c>, and <see cref="Data.DemoTenantAdminSeed"/>.
/// </summary>
public static class DemoTenantIds
{
    public static readonly Guid Dev = Guid.Parse("b0000001-0001-4001-8001-000000000001");
    public static readonly Guid Prod = Guid.Parse("b0000001-0001-4001-8001-000000000002");

    public static readonly IReadOnlyList<Guid> All = new[] { Dev, Prod };

    /// <summary>Local dev preset shown as "demo tenant" in Super Admin selectors (slug <c>dev</c> only).</summary>
    public static bool IsDemoPresetSlug(string? slug) =>
        string.Equals(slug?.Trim(), "dev", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Legacy cafe/bar demo rows replaced by <c>dev</c>/<c>prod</c>. Keep in DB for rollback;
/// hide from switcher and Super Admin cash-register lists. Not the platform sentinel.
/// </summary>
public static class LeftoverDemoTenantSlugs
{
    /// <summary>Storage forms used in EF filters (hyphen and underscore variants).</summary>
    public static readonly string[] StorageSlugs =
    [
        "cafe",
        "bar",
        "test-cafe",
        "test-bar",
        "test_cafe",
        "test_bar",
    ];

    public static bool Matches(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return false;

        var normalized = slug.Trim().ToLowerInvariant().Replace('_', '-');
        return normalized is "cafe" or "bar" or "test-cafe" or "test-bar";
    }
}

/// <summary>Stable Identity user ids for demo tenant administrators.</summary>
public static class DemoTenantAdminUserIds
{
    public const string Dev = "demo-tenant-admin-dev";
    public const string Prod = "demo-tenant-admin-prod";
}
