namespace KasseAPI_Final.Tenancy;

/// <summary>
/// Ambient tenant for the current HTTP request or background scope.
/// When <see cref="TenantId"/> is null, <see cref="Data.AppDbContext"/> <see cref="Models.ITenantEntity"/>
/// global query filters match no rows (fail-closed — never expose all tenants).
/// </summary>
public interface ICurrentTenantAccessor
{
    Guid? TenantId { get; set; }

    /// <summary>
    /// Optional slug for the ambient tenant (download filenames, logging).
    /// May be null when only <see cref="TenantId"/> was bound (e.g. background scopes).
    /// </summary>
    string? TenantSlug { get; set; }
}
