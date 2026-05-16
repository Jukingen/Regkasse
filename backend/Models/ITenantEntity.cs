namespace KasseAPI_Final.Models;

/// <summary>
/// Marks a row as belonging to a SaaS tenant. Used by <see cref="Data.AppDbContext"/> global query filters.
/// </summary>
public interface ITenantEntity
{
    /// <summary>FK to <see cref="Tenant"/> (<c>tenants.id</c>).</summary>
    Guid TenantId { get; set; }
}
