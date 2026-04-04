namespace KasseAPI_Final.Tenancy;

/// <summary>
/// Well-known identifiers for the seeded legacy tenant. Must match migration seed data.
/// </summary>
public static class LegacyDefaultTenantIds
{
    /// <summary>Primary row inserted by Wave 0–1 migrations.</summary>
    public static readonly Guid Primary = Guid.Parse("9c8f4e2b-1a3d-4f6e-8b7c-0d1e2f3a4b5c");

    public const string PrimarySlug = "default";
}
