using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace KasseAPI_Final.Models
{
    [Table("invoices")]
    public class Invoice : BaseEntity
    {
        [Required]
        [StringLength(50)]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Required]
        public DateTime InvoiceDate { get; set; }

        [Required]
        public DateTime DueDate { get; set; }

        [Required]
        public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

        [Required]
        public decimal Subtotal { get; set; }

        [Required]
        public decimal TaxAmount { get; set; }

        [Required]
        public decimal TotalAmount { get; set; }

        [Required]
        public decimal PaidAmount { get; set; }

        [Required]
        public decimal RemainingAmount { get; set; }

        // Müşteri Bilgileri
        [StringLength(100)]
        public string? CustomerName { get; set; }

        [StringLength(100)]
        public string? CustomerEmail { get; set; }

        [StringLength(20)]
        public string? CustomerPhone { get; set; }

        [StringLength(200)]
        public string? CustomerAddress { get; set; }

        [StringLength(20)]
        public string? CustomerTaxNumber { get; set; } // ATU12345678 formatı

        // Firma Bilgileri (RKSV zorunlu)
        [Required]
        [StringLength(100)]
        public string CompanyName { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string CompanyTaxNumber { get; set; } = string.Empty; // ATU12345678

        [Required]
        [StringLength(200)]
        public string CompanyAddress { get; set; } = string.Empty;

        [StringLength(20)]
        public string? CompanyPhone { get; set; }

        [StringLength(100)]
        public string? CompanyEmail { get; set; }

        // RKSV Zorunlu Alanlar
        [Required]
        [StringLength(500)]
        public string TseSignature { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string KassenId { get; set; } = string.Empty;

        [Required]
        public DateTime TseTimestamp { get; set; }

        // Cash Register ID for Tagesabschluss
        [Required]
        public Guid CashRegisterId { get; set; }

        // Ödeme Bilgileri
        public PaymentMethod? PaymentMethod { get; set; }

        [StringLength(50)]
        public string? PaymentReference { get; set; }

        public DateTime? PaymentDate { get; set; }

        // Fatura Detayları
        [Column(TypeName = "jsonb")]
        public JsonDocument? InvoiceItems { get; set; }

        [Column(TypeName = "jsonb")]
        [Required]
        public JsonDocument TaxDetails { get; set; } = JsonDocument.Parse("{}");

        // Source payment (POS backfill / auto-create idempotency key)
        // Null for manually-created invoices; set for POS-originated rows.
        public Guid? SourcePaymentId { get; set; }

        // Credit note / storno fields
        public DocumentType DocumentType { get; set; } = DocumentType.Invoice;
        public Guid? OriginalInvoiceId { get; set; }

        [StringLength(50)]
        public string? StornoReasonCode { get; set; }

        [StringLength(500)]
        public string? StornoReasonText { get; set; }
    }

    public enum InvoiceStatus
    {
        Draft,
        Sent,
        Paid,
        PartiallyPaid,
        Unpaid,
        Overdue,
        Cancelled,
        CreditNote = 7
    }

    public enum PaymentMethod
    {
        Cash = 0,
        Card = 1,
        BankTransfer = 2,
        Check = 3,
        Voucher = 4,
        Mobile = 5
    }

    public enum DocumentType
    {
        Invoice = 0,
        CreditNote = 1
    }
}
