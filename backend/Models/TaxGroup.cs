using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Tenant-scoped VAT / MwSt tax group catalog (Austrian rates including 4.9% and 13%).
/// Complements legacy product <see cref="TaxType"/> / <see cref="TaxTypes"/> ints; products may later reference this catalog.
/// </summary>
[Table("tax_groups")]
public class TaxGroup : BaseEntity, ITenantEntity
{
    /// <summary>FK to <see cref="Models.Tenant"/>.</summary>
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("description")]
    public string? Description { get; set; }

    /// <summary>VAT rate percent (e.g. 0, 4.9, 10, 13, 20). Calculation uses fraction = Rate/100.</summary>
    [Column("rate", TypeName = "decimal(5,2)")]
    [Range(0, 100, ErrorMessage = "Tax rate must be between 0 and 100")]
    public decimal Rate { get; set; }

    [Column("is_default")]
    public bool IsDefault { get; set; }

    /// <summary>When true, group cannot be deleted by tenant admins (system Austrian presets).</summary>
    [Column("is_system")]
    public bool IsSystem { get; set; }

    [MaxLength(20)]
    [Column("color")]
    public string? Color { get; set; }

    [MaxLength(50)]
    [Column("icon")]
    public string? Icon { get; set; }

    /// <summary>Canonical group kind for UI / mapping (optional for custom tenant rates).</summary>
    [Column("group_type")]
    public TaxGroupType? GroupType { get; set; }

    /// <summary>Austrian MwSt letter code when applicable (A–E).</summary>
    [MaxLength(8)]
    [Column("austrian_code")]
    public string? AustrianCode { get; set; }

    [Column("valid_from")]
    public DateTime? ValidFrom { get; set; }

    [Column("valid_to")]
    public DateTime? ValidTo { get; set; }

    /// <summary>When a rate is replaced historically, points at the successor tax group.</summary>
    [Column("replaced_by")]
    public Guid? ReplacedBy { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }

    [ForeignKey(nameof(ReplacedBy))]
    public virtual TaxGroup? ReplacedByGroup { get; set; }
}

/// <summary>
/// Flexible tax group kinds for Austrian MwSt (extends legacy Standard/Reduced/Special/ZeroRate).
/// </summary>
public enum TaxGroupType
{
    /// <summary>20% Normalsatz.</summary>
    Standard = 0,

    /// <summary>10% Ermäßigt.</summary>
    Reduced = 1,

    /// <summary>4.9% Ermäßigt (Neu).</summary>
    ReducedNew = 2,

    /// <summary>13% Mittelsteuersatz.</summary>
    Middle = 3,

    /// <summary>0% Nullsteuersatz.</summary>
    Zero = 4,
}
