using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class CouponUsage
{
    public Guid Id { get; set; }

    public Guid CouponId { get; set; }

    public Guid? CustomerId { get; set; }

    public Guid? InvoiceId { get; set; }

    public Guid? OrderId { get; set; }

    public decimal DiscountAmount { get; set; }

    public DateTime UsedAt { get; set; }

    public string UsedBy { get; set; } = null!;

    public string SessionId { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsActive { get; set; }

    public virtual Coupon Coupon { get; set; } = null!;

    public virtual Customer? Customer { get; set; }

    public virtual Invoice? Invoice { get; set; }

    public virtual Order? Order { get; set; }
}
