using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace KasseAPI_Final.Models
{
    [Table("payment_details")]
    public class PaymentDetails : BaseEntity
    {
        [Required]
        public Guid CustomerId { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string CustomerName { get; set; } = string.Empty;

        // Yeni eklenen alanlar - Frontend PaymentModal'dan gelen
        [Required]
        public int TableNumber { get; set; } // Masa numarası
        
        [Required]
        [MaxLength(100)]
        public string CashierId { get; set; } = string.Empty; // Kasiyer ID

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }
        
        // DB column "PaymentMethod" is varchar storing numeric strings like '0', '1', etc.
        [Required]
        [MaxLength(50)]
        [Column("PaymentMethod")]
        public string PaymentMethodRaw { get; set; } = "0";
        
        // Enum helper for type-safe access (not mapped to DB)
        [NotMapped]
        public PaymentMethod PaymentMethod
        {
            get
            {
                if (int.TryParse(PaymentMethodRaw, out int value) && Enum.IsDefined(typeof(PaymentMethod), value))
                    return (PaymentMethod)value;
                return PaymentMethod.Cash; // Default fallback
            }
            set
            {
                PaymentMethodRaw = ((int)value).ToString();
            }
        }
        
        // Avusturya yasal gereksinimleri (RKSV & DSGVO)
        [Required]
        [MaxLength(12)]
        [RegularExpression(@"^ATU\d{8}$", ErrorMessage = "Steuernummer formatı ATU12345678 olmalıdır")]
        public string Steuernummer { get; set; } = string.Empty; // Vergi numarası (ATU12345678)
        
        [Required]
        [MaxLength(50)]
        public string KassenId { get; set; } = string.Empty; // Kasa ID
        
        [MaxLength(500)]
        public string? Notes { get; set; }
        
        [MaxLength(100)]
        public string? TransactionId { get; set; }
        
        [Required]
        [MaxLength(2000)]
        public string TseSignature { get; set; } = string.Empty; // RKSV §6 COMPACT JWS
        
        [MaxLength(2000)]
        public string? PrevSignatureValueUsed { get; set; } // Imza zinciri için önceki signature
        
        [Required]
        public DateTime TseTimestamp { get; set; } // TSE zaman damgası

        // RKSV verification normalization (Phase 1) - nullable, eski kayıtlarla uyumlu
        [MaxLength(50)]
        [Column("signature_format")]
        public string? SignatureFormat { get; set; }

        [MaxLength(1000)]
        [Column("jws_header")]
        public string? JwsHeader { get; set; }

        [MaxLength(4000)]
        [Column("jws_payload")]
        public string? JwsPayload { get; set; }

        [MaxLength(500)]
        [Column("jws_signature")]
        public string? JwsSignature { get; set; }

        [MaxLength(50)]
        public string? Provider { get; set; }

        [MaxLength(100)]
        [Column("correlation_id")]
        public string? CorrelationId { get; set; }
        
        // Tax details as JSONB (PostgreSQL)
        [Column(TypeName = "jsonb")]
        public JsonDocument TaxDetails { get; set; } = JsonDocument.Parse("{}");
        
        // Payment items as JSONB
        [Column(TypeName = "jsonb")]
        public JsonDocument PaymentItems { get; set; } = JsonDocument.Parse("[]");
        
        // Receipt/Invoice fields
        [MaxLength(50)]
        public string ReceiptNumber { get; set; } = string.Empty; // Format: AT-{TSE_ID}-{YYYYMMDD}-{SEQ}
        
        public bool IsPrinted { get; set; } = false;
        
        // Navigation properties
        public virtual Customer? Customer { get; set; }
    }
}
