namespace KasseAPI_Final.Tenancy;

/// <summary>
/// Ambient tenant for the current HTTP request or background scope.
/// When <see cref="TenantId"/> is null, <see cref="Data.AppDbContext"/> <see cref="Models.ITenantEntity"/>
/// global query filters match no rows (fail-closed — never expose all tenants).
/// </summary>
public interface ICurrentTenantAccessor
{
    Guid? TenantId { get; set; }
}
