using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse_API.Models
{
    [Table("finance_online")]
    public class FinanceOnline : BaseEntity
    {
        [Required]
        [Column("transaction_number")]
        [MaxLength(50)]
        public string TransactionNumber { get; set; }

        [Required]
        [Column("invoice_id")]
        public Guid InvoiceId { get; set; }

        [Required]
        [Column("signature_certificate")]
        public string SignatureCertificate { get; set; }

        [Required]
        [Column("signature_value")]
        public string SignatureValue { get; set; }

        [Required]
        [Column("qr_code")]
        public string QRCode { get; set; }

        [Required]
        [Column("response_code")]
        [MaxLength(10)]
        public string ResponseCode { get; set; }

        [Required]
        [Column("response_message")]
        public string ResponseMessage { get; set; }

        // Navigation properties
        public virtual Invoice Invoice { get; set; }
    }

    public enum FinanceOnlineStatus
    {
        Pending,
        Submitted,
        Accepted,
        Rejected,
        Error
    }
} 
