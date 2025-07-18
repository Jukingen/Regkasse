using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class CustomerDiscount
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public string DiscountType { get; set; } = null!;

    public decimal DiscountValue { get; set; }

    public string Description { get; set; } = null!;

    public DateTime ValidFrom { get; set; }

    public DateTime? ValidUntil { get; set; }

    public int UsageLimit { get; set; }

    public int UsedCount { get; set; }

    public bool IsActive { get; set; }

    public string ProductCategoryRestriction { get; set; } = null!;

    public decimal MinimumAmount { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public virtual Customer Customer { get; set; } = null!;
}
