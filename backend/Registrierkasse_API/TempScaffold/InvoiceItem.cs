using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class InvoiceItem
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = null!;

    public string Description { get; set; } = null!;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal TaxRate { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public int TaxType { get; set; }

    public Guid InvoiceId { get; set; }

    public string Product { get; set; } = null!;

    public decimal DiscountAmount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsActive { get; set; }

    public virtual Invoice Invoice { get; set; } = null!;

    public virtual Product ProductNavigation { get; set; } = null!;
}
