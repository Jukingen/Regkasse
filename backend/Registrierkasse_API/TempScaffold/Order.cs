using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class Order
{
    public Guid Id { get; set; }

    public string OrderNumber { get; set; } = null!;

    public DateTime OrderDate { get; set; }

    public int Status { get; set; }

    public decimal TotalAmount { get; set; }

    public string? TableNumber { get; set; }

    public string? WaiterName { get; set; }

    public string? Notes { get; set; }

    public string? CustomerId { get; set; }

    public Guid? CustomerId1 { get; set; }

    public string? ApplicationUserId { get; set; }

    public Guid? TableId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsActive { get; set; }

    public virtual AspNetUser? ApplicationUser { get; set; }

    public virtual ICollection<CouponUsage> CouponUsages { get; set; } = new List<CouponUsage>();

    public virtual Customer? CustomerId1Navigation { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual Table? Table { get; set; }

    public virtual ICollection<Table> Tables { get; set; } = new List<Table>();
}
