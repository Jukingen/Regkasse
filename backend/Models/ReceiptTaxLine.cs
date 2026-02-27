using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("receipt_tax_lines")]
    public class ReceiptTaxLine
    {
        [Key]
        [Column("line_id")]
        public Guid LineId { get; set; } = Guid.NewGuid();

        [Required]
        [Column("receipt_id")]
        public Guid ReceiptId { get; set; }

        [Required]
        [Column("tax_rate", TypeName = "decimal(5, 2)")]
        public decimal TaxRate { get; set; }

        [Required]
        [Column("net_amount", TypeName = "decimal(10, 2)")]
        public decimal NetAmount { get; set; }

        [Required]
        [Column("tax_amount", TypeName = "decimal(10, 2)")]
        public decimal TaxAmount { get; set; }

        [Required]
        [Column("gross_amount", TypeName = "decimal(10, 2)")]
        public decimal GrossAmount { get; set; }

        // Navigation Property
        [ForeignKey("ReceiptId")]
        public virtual Receipt? Receipt { get; set; }
    }
}
