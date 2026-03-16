using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    /// <summary>
    /// Defines a customer benefit type (e.g. percentage discount, free daily allowance, buy X get Y).
    /// Minimal persistence for future wiring; not yet used by PaymentService.
    /// </summary>
    [Table("benefit_definitions")]
    public class BenefitDefinition : BaseEntity
    {
        [Required]
        [Column("code")]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [Column("name")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("benefit_kind")]
        public AppliedBenefitKind BenefitKind { get; set; }

        /// <summary>For PercentageDiscount: discount percentage (e.g. 10).</summary>
        [Column("percentage_value", TypeName = "decimal(5,2)")]
        public decimal? PercentageValue { get; set; }

        /// <summary>For FreeAllowance: quantity per scope (e.g. 2 per day).</summary>
        [Column("allowance_quantity")]
        public int? AllowanceQuantity { get; set; }

        /// <summary>For FreeAllowance: scope (e.g. "per_day").</summary>
        [Column("allowance_scope")]
        [MaxLength(50)]
        public string? AllowanceScope { get; set; }

        /// <summary>For FreeAllowance: only items in this category are eligible. Null = do not apply.</summary>
        [Column("allowance_category_id")]
        public Guid? AllowanceCategoryId { get; set; }

        /// <summary>For BuyXGetY: required quantity (e.g. 7).</summary>
        [Column("buy_x_quantity")]
        public int? BuyXQuantity { get; set; }

        /// <summary>For BuyXGetY: free quantity (e.g. 1).</summary>
        [Column("get_y_quantity")]
        public int? GetYQuantity { get; set; }

        [ForeignKey("AllowanceCategoryId")]
        public virtual Category? AllowanceCategory { get; set; }

        public virtual ICollection<BenefitAssignment> Assignments { get; set; } = new List<BenefitAssignment>();
    }
}
