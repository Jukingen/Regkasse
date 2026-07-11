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

/// <summary>Stable Identity user ids for demo tenant administrators.</summary>
public static class DemoTenantAdminUserIds
{
    public const string Dev = "demo-tenant-admin-dev";
    public const string Prod = "demo-tenant-admin-prod";
}
