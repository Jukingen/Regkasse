using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class Product
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public decimal Price { get; set; }

    public int TaxType { get; set; }

    public string Description { get; set; } = null!;

    public string Barcode { get; set; } = null!;

    public string Category { get; set; } = null!;

    public string? ImageUrl { get; set; }

    public int StockQuantity { get; set; }

    public int MinStockLevel { get; set; }

    public string Unit { get; set; } = null!;

    public decimal Cost { get; set; }

    public decimal TaxRate { get; set; }

    public string? CategoryId { get; set; }

    public Guid? CategoryId1 { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public virtual Category? CategoryId1Navigation { get; set; }

    public virtual Inventory? Inventory { get; set; }

    public virtual ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<ReceiptItem> ReceiptItems { get; set; } = new List<ReceiptItem>();
}
