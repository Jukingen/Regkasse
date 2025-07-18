using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class InventoryTransaction
{
    public Guid Id { get; set; }

    public string InventoryId { get; set; } = null!;

    public Guid? InventoryId1 { get; set; }

    public int QuantityChange { get; set; }

    public string? Reference { get; set; }

    public DateTime TransactionDate { get; set; }

    public string? ApplicationUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsActive { get; set; }

    public virtual AspNetUser? ApplicationUser { get; set; }

    public virtual Inventory? InventoryId1Navigation { get; set; }
}
