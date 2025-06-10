using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Registrierkasse.Models
{
    public enum InvoiceType
    {
        Standard,       // Normal satış fişi
        Refund,        // İade fişi
        Correction,    // Düzeltme fişi
        Void,          // İptal fişi
        DailyReport,   // Günlük rapor
        MonthlyReport, // Aylık rapor
        YearlyReport,  // Yıllık rapor
        Training,      // Eğitim modu fişi
        Test           // Test fişi
    }

    [Table("invoices")]
    public class Invoice : BaseEntity
    {
        [Required]
        [Column("invoice_number")]
        [MaxLength(20)]
        public string InvoiceNumber { get; set; } = string.Empty;
        
        [Required]
        [Column("cash_register_id")]
        public Guid CashRegisterId { get; set; }
        
        [ForeignKey("CashRegisterId")]
        public virtual CashRegister CashRegister { get; set; } = null!;
        
        [Column("customer_id")]
        public Guid? CustomerId { get; set; }
        
        [ForeignKey("CustomerId")]
        public virtual Customer? Customer { get; set; }
        
        [Column("order_id")]
        public Guid? OrderId { get; set; }
        
        [ForeignKey("OrderId")]
        public virtual Order? Order { get; set; }
        
        [Required]
        [Column("invoice_date")]
        public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
        
        [Required]
        [Column("total_amount")]
        public decimal TotalAmount { get; set; }
        
        [Required]
        [Column("tax_amount")]
        public decimal TaxAmount { get; set; }
        
        [Required]
        [Column("payment_method")]
        [MaxLength(20)]
        public PaymentMethod PaymentMethod { get; set; }
        
        [Column("waiter_name")]
        [MaxLength(100)]
        public string WaiterName { get; set; } = string.Empty;
        
        [Column("notes")]
        [MaxLength(500)]
        public string Notes { get; set; } = string.Empty;
        
        [Column("is_active")]
        public new bool IsActive { get; set; } = true;
        
        [Column("receipt_number")]
        [MaxLength(20)]
        public string ReceiptNumber { get; set; } = string.Empty;
        
        [Column("invoice_type")]
        [MaxLength(20)]
        public string InvoiceType { get; set; }
        
        [Column("tax_details")]
        public JsonDocument TaxDetails { get; set; } = null!;
        
        [Column("tax_summary")]
        public JsonDocument TaxSummary { get; set; } = null!;
        
        [Column("payment_details")]
        public JsonDocument PaymentDetails { get; set; } = null!;
        
        [Column("customer_details")]
        public JsonDocument? CustomerDetails { get; set; }
        
        [Column("is_printed")]
        public bool IsPrinted { get; set; }
        
        [Column("is_electronic")]
        public bool IsElectronic { get; set; }
        
        [Column("is_void")]
        public bool IsVoid { get; set; }
        
        [Column("void_reason")]
        [MaxLength(500)]
        public string? VoidReason { get; set; }
        
        [Column("original_invoice_id")]
        public Guid? OriginalInvoiceId { get; set; }
        
        [ForeignKey("OriginalInvoiceId")]
        public virtual Invoice? OriginalInvoice { get; set; }
        
        [Column("related_invoice_ids")]
        public JsonDocument? RelatedInvoiceIds { get; set; }
        
        [Column("tse_signature")]
        [MaxLength(255)]
        public string TseSignature { get; set; } = string.Empty;
        
        [Column("tse_signature_counter")]
        public long TseSignatureCounter { get; set; }
        
        [Column("tse_time")]
        public DateTime TseTime { get; set; }
        
        [Column("tse_serial_number")]
        [MaxLength(100)]
        public string TseSerialNumber { get; set; } = string.Empty;
        
        [Column("tse_certificate")]
        [MaxLength(1000)]
        public string TseCertificate { get; set; } = string.Empty;
        
        [Column("tse_process_type")]
        [MaxLength(50)]
        public string TseProcessType { get; set; } = "SIGN";
        
        [Column("tse_process_data")]
        public JsonDocument? TseProcessData { get; set; }
        
        [Column("qr_code")]
        [MaxLength(1000)]
        public string QrCode { get; set; } = string.Empty;
        
        [Column("status")]
        [MaxLength(20)]
        public string Status { get; set; } = InvoiceStatus.Pending.ToString();
        
        public virtual ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
        
        public virtual FinanceOnline? FinanceOnline { get; set; }
    }

    public enum InvoiceStatus
    {
        Pending,
        Completed,
        Cancelled,
        Refunded
    }

    public class TaxSummary
    {
        public decimal StandardTaxBase { get; set; }
        public decimal StandardTaxAmount { get; set; }
        public decimal ReducedTaxBase { get; set; }
        public decimal ReducedTaxAmount { get; set; }
        public decimal SpecialTaxBase { get; set; }
        public decimal SpecialTaxAmount { get; set; }
        public decimal ZeroTaxBase { get; set; }
        public decimal ExemptTaxBase { get; set; }
        public decimal TotalTaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class PaymentDetails
    {
        public string PaymentMethod { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "EUR";
        public string? CardType { get; set; }
        public string? CardLastDigits { get; set; }
        public string? TransactionId { get; set; }
        public string? VoucherCode { get; set; }
        public decimal? VoucherAmount { get; set; }
        public decimal? CashAmount { get; set; }
        public decimal? CardAmount { get; set; }
        public decimal? ChangeAmount { get; set; }
    }

    public class CustomerDetails
    {
        public string? CustomerId { get; set; }
        public string? TaxNumber { get; set; }
        public string? CompanyName { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? VatNumber { get; set; }
    }
} 