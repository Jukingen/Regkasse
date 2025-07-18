using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class CompanySetting
{
    public Guid Id { get; set; }

    public string CompanyName { get; set; } = null!;

    public string TaxNumber { get; set; } = null!;

    public string? Vatnumber { get; set; }

    public string? Address { get; set; }

    public string? City { get; set; }

    public string? PostalCode { get; set; }

    public string? Country { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Website { get; set; }

    public string? BankName { get; set; }

    public string? BankAccount { get; set; }

    public string? Iban { get; set; }

    public string? Bic { get; set; }

    public string? Logo { get; set; }

    public string? InvoiceFooter { get; set; }

    public string? ReceiptFooter { get; set; }

    public string? DefaultCurrency { get; set; }

    public string? Industry { get; set; }

    public string? FinanceOnlineUsername { get; set; }

    public string? FinanceOnlinePassword { get; set; }

    public string? SignatureCertificate { get; set; }

    public decimal DefaultTaxRate { get; set; }

    public bool IsFinanceOnlineEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsActive { get; set; }
}
