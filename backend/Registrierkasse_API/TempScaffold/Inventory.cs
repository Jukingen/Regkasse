using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class Inventory
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public int CurrentStock { get; set; }

    public int MinimumStock { get; set; }

    public int MaximumStock { get; set; }

    public DateTime? LastStockUpdate { get; set; }

    public string? Notes { get; set; }

    public string? Location { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();

    public virtual Product Product { get; set; } = null!;
}
