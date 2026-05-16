using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Base for tenant-scoped entities that also use <see cref="BaseEntity"/> audit columns.
/// </summary>
public abstract class BaseTenantEntity : BaseEntity, ITenantEntity
{
    [Column("tenant_id")]
    public Guid TenantId { get; set; }
}
