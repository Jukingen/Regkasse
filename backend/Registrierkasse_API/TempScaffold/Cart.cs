using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class Cart
{
    public string CartId { get; set; } = null!;

    public string? TableNumber { get; set; }

    public string? WaiterName { get; set; }

    public Guid? CustomerId { get; set; }

    public string? UserId { get; set; }

    public Guid? CashRegisterId { get; set; }

    public decimal Subtotal { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public Guid? AppliedCouponId { get; set; }

    public string? Notes { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Coupon? AppliedCoupon { get; set; }

    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public virtual CashRegister? CashRegister { get; set; }

    public virtual Customer? Customer { get; set; }

    public virtual AspNetUser? User { get; set; }
}
