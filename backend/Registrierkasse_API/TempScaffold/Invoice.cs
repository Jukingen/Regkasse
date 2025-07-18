using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class Invoice
{
    public Guid Id { get; set; }

    public string InvoiceNumber { get; set; } = null!;

    public DateTime InvoiceDate { get; set; }

    public DateTime DueDate { get; set; }

    public int Status { get; set; }

    public decimal Subtotal { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal PaidAmount { get; set; }

    public decimal RemainingAmount { get; set; }

    public string? CustomerName { get; set; }

    public string? CustomerEmail { get; set; }

    public string? CustomerPhone { get; set; }

    public string? CustomerAddress { get; set; }

    public string? CustomerTaxNumber { get; set; }

    public string CompanyName { get; set; } = null!;

    public string CompanyTaxNumber { get; set; } = null!;

    public string CompanyAddress { get; set; } = null!;

    public string? CompanyPhone { get; set; }

    public string? CompanyEmail { get; set; }

    public string TseSignature { get; set; } = null!;

    public string KassenId { get; set; } = null!;

    public DateTime TseTimestamp { get; set; }

    public int? PaymentMethod { get; set; }

    public string? PaymentReference { get; set; }

    public DateTime? PaymentDate { get; set; }

    public string? InvoiceItems { get; set; }

    public string TaxDetails { get; set; } = null!;

    public string? Notes { get; set; }

    public string? TermsAndConditions { get; set; }

    public bool IsSubmittedToFinanzOnline { get; set; }

    public DateTime? FinanzOnlineSubmissionDate { get; set; }

    public string? FinanzOnlineReference { get; set; }

    public string? CustomerId { get; set; }

    public string CreatedById { get; set; } = null!;

    public string ReceiptNumber { get; set; } = null!;

    public bool IsPrinted { get; set; }

    public int? PaymentStatus { get; set; }

    public string? InvoiceType { get; set; }

    public string? CashRegisterId { get; set; }

    public string? CancelledReason { get; set; }

    public DateTime? CancelledDate { get; set; }

    public DateTime? SentDate { get; set; }

    public string? UpdatedById { get; set; }

    public bool FinanzOnlineSubmitted { get; set; }

    public Guid? TaxSummaryId { get; set; }

    public Guid? PaymentDetailsId { get; set; }

    public Guid? CustomerDetailsId { get; set; }

    public Guid? CashRegisterId1 { get; set; }

    public Guid? CustomerId1 { get; set; }

    public Guid? InvoiceTemplateId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsActive { get; set; }

    public virtual CashRegister? CashRegisterId1Navigation { get; set; }

    public virtual ICollection<CouponUsage> CouponUsages { get; set; } = new List<CouponUsage>();

    public virtual AspNetUser CreatedBy { get; set; } = null!;

    public virtual AspNetUser? Customer { get; set; }

    public virtual CustomerDetail? CustomerDetails { get; set; }

    public virtual Customer? CustomerId1Navigation { get; set; }

    public virtual ICollection<FinanceOnline> FinanceOnlines { get; set; } = new List<FinanceOnline>();

    public virtual ICollection<InvoiceHistory> InvoiceHistories { get; set; } = new List<InvoiceHistory>();

    public virtual ICollection<InvoiceItem> InvoiceItemsNavigation { get; set; } = new List<InvoiceItem>();

    public virtual InvoiceTemplate? InvoiceTemplate { get; set; }

    public virtual PaymentDetail? PaymentDetails { get; set; }

    public virtual TaxSummary? TaxSummary { get; set; }

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
