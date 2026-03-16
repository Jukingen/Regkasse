using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    /// <summary>
    /// Tracks daily usage of free-allowance benefits per customer and definition. Used to enforce daily limits.
    /// One row per (CustomerId, BenefitDefinitionId, UsageDate); QuantityUsed is incremented when allowance is applied.
    /// </summary>
    [Table("benefit_daily_usage")]
    public class BenefitDailyUsage
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("customer_id")]
        public Guid CustomerId { get; set; }

        [Required]
        [Column("benefit_definition_id")]
        public Guid BenefitDefinitionId { get; set; }

        [Required]
        [Column("usage_date", TypeName = "date")]
        public DateTime UsageDate { get; set; }

        [Required]
        [Column("quantity_used")]
        public int QuantityUsed { get; set; }

        /// <summary>Concurrency token so concurrent payments do not over-consume daily allowance.</summary>
        [ConcurrencyCheck]
        [Column("version")]
        public int Version { get; set; }

        [ForeignKey("CustomerId")]
        public virtual Customer Customer { get; set; } = null!;

        [ForeignKey("BenefitDefinitionId")]
        public virtual BenefitDefinition BenefitDefinition { get; set; } = null!;
    }
}
