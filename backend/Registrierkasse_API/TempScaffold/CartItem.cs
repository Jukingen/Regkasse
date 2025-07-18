using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class CartItem
{
    public Guid Id { get; set; }

    public string CartId { get; set; } = null!;

    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = null!;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal TaxRate { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string? Notes { get; set; }

    public bool IsModified { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public decimal OriginalUnitPrice { get; set; }

    public int OriginalQuantity { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsActive { get; set; }

    public virtual Cart Cart { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;
}
