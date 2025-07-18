using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class OrderItem
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal TotalAmount { get; set; }

    public string? Notes { get; set; }

    public string OrderId { get; set; } = null!;

    public Guid OrderId1 { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsActive { get; set; }

    public virtual Order OrderId1Navigation { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;
}
