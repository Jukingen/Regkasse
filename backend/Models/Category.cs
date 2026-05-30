using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("categories")]
    public class Category : BaseEntity, ITenantEntity
    {
        /// <summary>FK to <see cref="Models.Tenant"/>; category keys are unique per tenant.</summary>
        [Column("tenant_id")]
        public Guid TenantId { get; set; }

        /// <summary>Immutable internal identifier (slug). Never changes after creation.</summary>
        [Required]
        [MaxLength(100)]
        [Column("category_key")]
        public string Key { get; set; } = string.Empty;

        /// <summary>User-editable display name for UI only.</summary>
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Original demo catalog name; used for reset-to-default display name.</summary>
        [MaxLength(100)]
        [Column("original_demo_name")]
        public string? OriginalDemoName { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(20)]
        public string? Color { get; set; }

        [MaxLength(50)]
        public string? Icon { get; set; }

        public int SortOrder { get; set; } = 0;

        /// <summary>Default VAT rate percent (e.g. 10, 20). Calculation uses fraction = VatRate/100.</summary>
        [Column("vat_rate", TypeName = "decimal(5,2)")]
        [Range(0, 100, ErrorMessage = "VAT rate must be between 0 and 100")]
        public decimal VatRate { get; set; } = 20m;

        /// <summary>RKSV fiscal category; immutable after creation for compliance.</summary>
        [Column("fiscal_category")]
        public RksvProductCategory FiscalCategory { get; set; } = RksvProductCategory.Food;

        /// <summary>When true, category cannot be deleted by tenant admins (demo/system categories).</summary>
        [Column("is_system_category")]
        public bool IsSystemCategory { get; set; }

        [ForeignKey(nameof(TenantId))]
        public virtual Tenant? Tenant { get; set; }

        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
