using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class ReceiptItem
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public decimal Price { get; set; }

    public decimal Total { get; set; }

    public decimal TaxAmount { get; set; }

    public int TaxType { get; set; }

    public decimal TaxRate { get; set; }

    public string ProductName { get; set; } = null!;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal TotalAmount { get; set; }

    public Guid ReceiptId { get; set; }

    public Guid? ProductId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsActive { get; set; }

    public virtual Product? Product { get; set; }

    public virtual Receipt Receipt { get; set; } = null!;
}
