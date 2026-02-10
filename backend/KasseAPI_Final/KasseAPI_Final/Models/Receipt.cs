using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("receipts")]
    public class Receipt
    {
        [Key]
        [Column("receipt_id")]
        public Guid ReceiptId { get; set; } = Guid.NewGuid();

        [Required]
        [Column("payment_id")]
        public Guid PaymentId { get; set; }

        [Required]
        [Column("receipt_number")]
        [StringLength(50)]
        public string ReceiptNumber { get; set; } = string.Empty;

        [Required]
        [Column("issued_at")]
        public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

        [Column("cashier_id")]
        [StringLength(50)]
        public string? CashierId { get; set; }

        [Required]
        [Column("cash_register_id")]
        [StringLength(50)]
        public string CashRegisterId { get; set; } = string.Empty;

        [Required]
        [Column("sub_total", TypeName = "decimal(10, 2)")]
        public decimal SubTotal { get; set; }

        [Required]
        [Column("tax_total", TypeName = "decimal(10, 2)")]
        public decimal TaxTotal { get; set; }

        [Required]
        [Column("grand_total", TypeName = "decimal(10, 2)")]
        public decimal GrandTotal { get; set; }

        [Column("qr_code_payload")]
        public string? QrCodePayload { get; set; }

        [Column("signature_value")]
        public string? SignatureValue { get; set; }

        [Column("prev_signature_value")]
        public string? PrevSignatureValue { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("PaymentId")]
        public virtual PaymentDetails? Payment { get; set; }

        public virtual ICollection<ReceiptItem> Items { get; set; } = new List<ReceiptItem>();
        public virtual ICollection<ReceiptTaxLine> TaxLines { get; set; } = new List<ReceiptTaxLine>();
    }
}
