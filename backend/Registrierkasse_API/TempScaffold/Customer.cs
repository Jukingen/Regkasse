using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class Customer
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string CustomerNumber { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string Address { get; set; } = null!;

    public string TaxNumber { get; set; } = null!;

    public string CustomerCategory { get; set; } = null!;

    public int LoyaltyPoints { get; set; }

    public decimal TotalSpent { get; set; }

    public int VisitCount { get; set; }

    public DateTime? LastVisit { get; set; }

    public string Notes { get; set; } = null!;

    public bool IsVip { get; set; }

    public decimal DiscountPercentage { get; set; }

    public DateTime? BirthDate { get; set; }

    public string PreferredPaymentMethod { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();

    public virtual ICollection<CouponUsage> CouponUsages { get; set; } = new List<CouponUsage>();

    public virtual ICollection<CustomerDiscount> CustomerDiscounts { get; set; } = new List<CustomerDiscount>();

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
