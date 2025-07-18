using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class InvoiceTemplate
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; }

    public string CompanyName { get; set; } = null!;

    public string CompanyTaxNumber { get; set; } = null!;

    public string CompanyAddress { get; set; } = null!;

    public string? CompanyPhone { get; set; }

    public string? CompanyEmail { get; set; }

    public string? CompanyWebsite { get; set; }

    public string? LogoUrl { get; set; }

    public string PrimaryColor { get; set; } = null!;

    public string SecondaryColor { get; set; } = null!;

    public string FontFamily { get; set; } = null!;

    public string InvoiceTitle { get; set; } = null!;

    public string InvoiceNumberLabel { get; set; } = null!;

    public string InvoiceDateLabel { get; set; } = null!;

    public string DueDateLabel { get; set; } = null!;

    public string CustomerSectionTitle { get; set; } = null!;

    public string CustomerNameLabel { get; set; } = null!;

    public string CustomerEmailLabel { get; set; } = null!;

    public string CustomerPhoneLabel { get; set; } = null!;

    public string CustomerAddressLabel { get; set; } = null!;

    public string CustomerTaxNumberLabel { get; set; } = null!;

    public string ItemHeader { get; set; } = null!;

    public string DescriptionHeader { get; set; } = null!;

    public string QuantityHeader { get; set; } = null!;

    public string UnitPriceHeader { get; set; } = null!;

    public string TaxHeader { get; set; } = null!;

    public string TotalHeader { get; set; } = null!;

    public string SubtotalLabel { get; set; } = null!;

    public string TaxLabel { get; set; } = null!;

    public string TotalLabel { get; set; } = null!;

    public string PaidLabel { get; set; } = null!;

    public string RemainingLabel { get; set; } = null!;

    public string PaymentSectionTitle { get; set; } = null!;

    public string PaymentMethodLabel { get; set; } = null!;

    public string PaymentReferenceLabel { get; set; } = null!;

    public string PaymentDateLabel { get; set; } = null!;

    public string? FooterText { get; set; }

    public string? TermsAndConditions { get; set; }

    public string? Notes { get; set; }

    public string TseSignatureLabel { get; set; } = null!;

    public string KassenIdLabel { get; set; } = null!;

    public string TseTimestampLabel { get; set; } = null!;

    public bool ShowLogo { get; set; }

    public bool ShowCompanyInfo { get; set; }

    public bool ShowCustomerInfo { get; set; }

    public bool ShowPaymentInfo { get; set; }

    public bool ShowTseInfo { get; set; }

    public bool ShowTermsAndConditions { get; set; }

    public bool ShowNotes { get; set; }

    public string PageSize { get; set; } = null!;

    public int MarginTop { get; set; }

    public int MarginBottom { get; set; }

    public int MarginLeft { get; set; }

    public int MarginRight { get; set; }

    public string CreatedById { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public virtual AspNetUser CreatedBy { get; set; } = null!;

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
