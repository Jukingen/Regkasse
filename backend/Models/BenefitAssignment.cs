using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    /// <summary>
    /// Assigns a benefit definition to a customer. Minimal persistence for future wiring; not yet used by PaymentService.
    /// Extensible later for segment/group assignment (e.g. nullable CustomerId + SegmentId).
    /// </summary>
    [Table("benefit_assignments")]
    public class BenefitAssignment : BaseEntity
    {
        [Required]
        [Column("benefit_definition_id")]
        public Guid BenefitDefinitionId { get; set; }

        [Required]
        [Column("customer_id")]
        public Guid CustomerId { get; set; }

        [Required]
        [Column("valid_from")]
        public DateTime ValidFrom { get; set; }

        [Column("valid_to")]
        public DateTime? ValidTo { get; set; }

        [Column("priority")]
        public int Priority { get; set; }

        [ForeignKey("BenefitDefinitionId")]
        public virtual BenefitDefinition BenefitDefinition { get; set; } = null!;

        [ForeignKey("CustomerId")]
        public virtual Customer Customer { get; set; } = null!;
    }
}
