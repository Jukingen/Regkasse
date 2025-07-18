using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class TaxSummary
{
    public Guid Id { get; set; }

    public decimal StandardTaxBase { get; set; }

    public decimal StandardTaxAmount { get; set; }

    public decimal ReducedTaxBase { get; set; }

    public decimal ReducedTaxAmount { get; set; }

    public decimal SpecialTaxBase { get; set; }

    public decimal SpecialTaxAmount { get; set; }

    public decimal ZeroTaxBase { get; set; }

    public decimal ExemptTaxBase { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal TotalTaxAmount { get; set; }

    public decimal Standard { get; set; }

    public decimal Reduced { get; set; }

    public decimal Special { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
