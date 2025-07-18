using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Registrierkasse_API.Models
{
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
        [TaxDetailsValidation]
        public JsonDocument? TaxDetails { get; set; }

        // Notlar ve Açıklamalar
        [StringLength(500)]
        public string? Notes { get; set; }

        [StringLength(500)]
        public string? TermsAndConditions { get; set; }

        // FinanzOnline Entegrasyonu
        public bool IsSubmittedToFinanzOnline { get; set; } = false;

        public DateTime? FinanzOnlineSubmissionDate { get; set; }

        [StringLength(100)]
        public string? FinanzOnlineReference { get; set; }

        // İlişkiler
        public string? CustomerId { get; set; }
        public virtual ApplicationUser? Customer { get; set; }

        public string CreatedById { get; set; } = string.Empty;
        public virtual ApplicationUser CreatedBy { get; set; } = null!;

        // Fatura Geçmişi
        public virtual ICollection<InvoiceHistory> InvoiceHistory { get; set; } = new List<InvoiceHistory>();

        // Yardımcı Metodlar
        public void CalculateTotals()
        {
            if (InvoiceItems != null)
            {
                var items = JsonSerializer.Deserialize<List<InvoiceItem>>(InvoiceItems.RootElement.ToString());
                if (items != null)
                {
                    Subtotal = items.Sum(item => item.Quantity * item.UnitPrice);
                    TaxAmount = items.Sum(item => item.TaxAmount);
                    TotalAmount = Subtotal + TaxAmount;
                    RemainingAmount = TotalAmount - PaidAmount;
                }
            }
        }

        public void MarkAsPaid(decimal amount, string paymentMethod, string? reference = null)
        {
            PaidAmount += amount;
            RemainingAmount = TotalAmount - PaidAmount;
            if (!string.IsNullOrEmpty(paymentMethod))
            {
                PaymentMethod = Enum.Parse<PaymentMethod>(paymentMethod, true);
            }
            PaymentReference = reference;
            PaymentDate = DateTime.UtcNow;

            if (RemainingAmount <= 0)
            {
                Status = InvoiceStatus.Paid;
            }
        }

        public bool IsOverdue()
        {
            return Status != InvoiceStatus.Paid && Status != InvoiceStatus.Cancelled && DueDate < DateTime.UtcNow;
        }

        [Required]
        [StringLength(50)]
        [RegularExpression(@"^\d{8}-[0-9a-fA-F\-]{36}$", ErrorMessage = "Fiş numarası formatı: {tarih}-{uuid} olmalı.")]
        public string ReceiptNumber { get; set; } = string.Empty; // FİŞ_NO: {tarih}-{uuid}

        public bool IsPrinted { get; set; } = false;

        public PaymentStatus? PaymentStatus { get; set; }

        [StringLength(50)]
        public string? InvoiceType { get; set; } // e.g. "invoice", "credit_note"

        [StringLength(50)]
        public string? CashRegisterId { get; set; }

        [StringLength(200)]
        public string? CancelledReason { get; set; }
        public DateTime? CancelledDate { get; set; }

        public DateTime? SentDate { get; set; }
        public string? UpdatedById { get; set; }
        public bool FinanzOnlineSubmitted { get; set; } = false;

        // Navigation
        public virtual TaxSummary? TaxSummary { get; set; }
        public virtual PaymentDetails? PaymentDetails { get; set; }
        public virtual CustomerDetails? CustomerDetails { get; set; }
        public virtual ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
    }

    public class InvoiceHistory : BaseEntity
    {
        [Required]
        public Guid InvoiceId { get; set; }
        public virtual Invoice Invoice { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string Action { get; set; } = string.Empty; // created, sent, paid, cancelled, etc.

        [Required]
        [StringLength(200)]
        public string Description { get; set; } = string.Empty;

        [Column(TypeName = "jsonb")]
        public JsonDocument? Changes { get; set; }

        public string? PerformedById { get; set; }
        public virtual ApplicationUser? PerformedBy { get; set; }
    }

    public class TaxDetailsValidationAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value is JsonDocument jsonDoc)
            {
                var root = jsonDoc.RootElement;
                if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var property in root.EnumerateObject())
                    {
                        var key = property.Name;
                        if (key != "standard" && key != "reduced" && key != "special")
                        {
                            return new ValidationResult($"TaxDetails sadece 'standard', 'reduced', 'special' anahtarlarını içermelidir.");
                        }
                    }
                }
            }
            return ValidationResult.Success;
        }
    }
} 
