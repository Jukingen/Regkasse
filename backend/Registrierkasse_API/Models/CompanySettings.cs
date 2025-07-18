using System.ComponentModel.DataAnnotations;

namespace Registrierkasse_API.Models
{
    public class CompanySettings : BaseEntity
    {
        [Required]
        public string CompanyName { get; set; } = string.Empty;
        public string TaxNumber { get; set; } = string.Empty;
        public string? VATNumber { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Website { get; set; }
        public string? BankName { get; set; }
        public string? BankAccount { get; set; }
        public string? IBAN { get; set; }
        public string? BIC { get; set; }
        public string? Logo { get; set; }
        public string? InvoiceFooter { get; set; }
        public string? ReceiptFooter { get; set; }
        public string? DefaultCurrency { get; set; }
        public string? Industry { get; set; }
        public string? FinanceOnlineUsername { get; set; }
        public string? FinanceOnlinePassword { get; set; }
        public string? SignatureCertificate { get; set; }
        
        // Eksik property'ler
        public decimal DefaultTaxRate { get; set; } = 20.0m;
        public bool IsFinanceOnlineEnabled { get; set; } = false;
    }
} 
