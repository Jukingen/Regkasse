using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse_API.Models
{
    public class InvoiceTemplate : BaseEntity
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        public bool IsDefault { get; set; } = false;

        [Required]
        public bool IsActive { get; set; } = true;

        // Firma Bilgileri
        [Required]
        [StringLength(100)]
        public string CompanyName { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string CompanyTaxNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string CompanyAddress { get; set; } = string.Empty;

        [StringLength(20)]
        public string? CompanyPhone { get; set; }

        [StringLength(100)]
        public string? CompanyEmail { get; set; }

        [StringLength(100)]
        public string? CompanyWebsite { get; set; }

        // Logo ve Tasarım
        [StringLength(500)]
        public string? LogoUrl { get; set; }

        [StringLength(7)]
        public string PrimaryColor { get; set; } = "#007AFF";

        [StringLength(7)]
        public string SecondaryColor { get; set; } = "#5856D6";

        [StringLength(20)]
        public string FontFamily { get; set; } = "Arial";

        // Fatura Başlığı
        [StringLength(100)]
        public string InvoiceTitle { get; set; } = "INVOICE";

        [StringLength(100)]
        public string InvoiceNumberLabel { get; set; } = "Invoice Number:";

        [StringLength(100)]
        public string InvoiceDateLabel { get; set; } = "Invoice Date:";

        [StringLength(100)]
        public string DueDateLabel { get; set; } = "Due Date:";

        // Müşteri Bilgileri
        [StringLength(100)]
        public string CustomerSectionTitle { get; set; } = "Bill To:";

        [StringLength(100)]
        public string CustomerNameLabel { get; set; } = "Name:";

        [StringLength(100)]
        public string CustomerEmailLabel { get; set; } = "Email:";

        [StringLength(100)]
        public string CustomerPhoneLabel { get; set; } = "Phone:";

        [StringLength(100)]
        public string CustomerAddressLabel { get; set; } = "Address:";

        [StringLength(100)]
        public string CustomerTaxNumberLabel { get; set; } = "Tax Number:";

        // Tablo Başlıkları
        [StringLength(100)]
        public string ItemHeader { get; set; } = "Item";

        [StringLength(100)]
        public string DescriptionHeader { get; set; } = "Description";

        [StringLength(100)]
        public string QuantityHeader { get; set; } = "Qty";

        [StringLength(100)]
        public string UnitPriceHeader { get; set; } = "Unit Price";

        [StringLength(100)]
        public string TaxHeader { get; set; } = "Tax";

        [StringLength(100)]
        public string TotalHeader { get; set; } = "Total";

        // Özet Bölümü
        [StringLength(100)]
        public string SubtotalLabel { get; set; } = "Subtotal:";

        [StringLength(100)]
        public string TaxLabel { get; set; } = "Tax:";

        [StringLength(100)]
        public string TotalLabel { get; set; } = "Total:";

        [StringLength(100)]
        public string PaidLabel { get; set; } = "Paid:";

        [StringLength(100)]
        public string RemainingLabel { get; set; } = "Remaining:";

        // Ödeme Bilgileri
        [StringLength(100)]
        public string PaymentSectionTitle { get; set; } = "Payment Information";

        [StringLength(100)]
        public string PaymentMethodLabel { get; set; } = "Payment Method:";

        [StringLength(100)]
        public string PaymentReferenceLabel { get; set; } = "Reference:";

        [StringLength(100)]
        public string PaymentDateLabel { get; set; } = "Payment Date:";

        // Alt Bilgi
        [StringLength(500)]
        public string? FooterText { get; set; }

        [StringLength(500)]
        public string? TermsAndConditions { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        // RKSV Zorunlu Alanlar
        [StringLength(100)]
        public string TseSignatureLabel { get; set; } = "TSE Signature:";

        [StringLength(100)]
        public string KassenIdLabel { get; set; } = "Kassen ID:";

        [StringLength(100)]
        public string TseTimestampLabel { get; set; } = "TSE Timestamp:";

        // Şablon Ayarları
        public bool ShowLogo { get; set; } = true;
        public bool ShowCompanyInfo { get; set; } = true;
        public bool ShowCustomerInfo { get; set; } = true;
        public bool ShowPaymentInfo { get; set; } = true;
        public bool ShowTseInfo { get; set; } = true;
        public bool ShowTermsAndConditions { get; set; } = true;
        public bool ShowNotes { get; set; } = true;

        // Sayfa Ayarları
        [StringLength(10)]
        public string PageSize { get; set; } = "A4";

        public int MarginTop { get; set; } = 20;
        public int MarginBottom { get; set; } = 20;
        public int MarginLeft { get; set; } = 20;
        public int MarginRight { get; set; } = 20;

        // İlişkiler
        public string CreatedById { get; set; } = string.Empty;
        public virtual ApplicationUser CreatedBy { get; set; } = null!;

        public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    }
} 
