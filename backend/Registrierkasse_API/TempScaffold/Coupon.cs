using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class Coupon
{
    public Guid Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string DiscountType { get; set; } = null!;

    public decimal DiscountValue { get; set; }

    public decimal MinimumAmount { get; set; }

    public decimal MaximumDiscount { get; set; }

    public DateTime ValidFrom { get; set; }

    public DateTime ValidUntil { get; set; }

    public int UsageLimit { get; set; }

    public int UsedCount { get; set; }

    public bool IsActive { get; set; }

    public bool IsSingleUse { get; set; }

    public string? CustomerCategoryRestriction { get; set; }

    public string ProductCategoryRestriction { get; set; } = null!;

    public string CreatedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();

    public virtual ICollection<CouponUsage> CouponUsages { get; set; } = new List<CouponUsage>();
}
