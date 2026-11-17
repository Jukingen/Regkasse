using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>Mandant supplier contact row. Not a fiscal document.</summary>
[Table("suppliers")]
public class Supplier : BaseEntity, ITenantEntity
{
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required]
    [Column("name")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Column("email")]
    [MaxLength(200)]
    public string? Email { get; set; }

    [Column("phone")]
    [MaxLength(50)]
    public string? Phone { get; set; }

    [Column("address")]
    [MaxLength(500)]
    public string? Address { get; set; }
}
