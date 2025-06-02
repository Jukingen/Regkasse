using System;

namespace Registrierkasse.Models
{
    public class CompanySettings : BaseEntity
    {
        public string CompanyName { get; set; }
        public string TaxNumber { get; set; }
        public string VATNumber { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string PostalCode { get; set; }
        public string Country { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Website { get; set; }
        public string BankName { get; set; }
        public string BankAccount { get; set; }
        public string IBAN { get; set; }
        public string BIC { get; set; }
        public string Logo { get; set; }
        public string InvoiceFooter { get; set; }
        public string ReceiptFooter { get; set; }
        public string DefaultCurrency { get; set; }
        public decimal DefaultTaxRate { get; set; }
        public string Industry { get; set; }
        public bool IsFinanceOnlineEnabled { get; set; }
        public string FinanceOnlineUsername { get; set; }
        public string FinanceOnlinePassword { get; set; }
        public string SignatureCertificate { get; set; }
    }
} 