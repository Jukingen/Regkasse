using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("customers")]
    public class Customer : BaseEntity
    {
        [Required]
        [Column("name")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Column("customer_number")]
        [MaxLength(20)]
        public string CustomerNumber { get; set; } = string.Empty;

        [Column("email")]
        [EmailAddress]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [Column("phone")]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Column("address")]
        [MaxLength(200)]
        public string Address { get; set; } = string.Empty;

        [Column("tax_number")]
        [MaxLength(20)]
        public string TaxNumber { get; set; } = string.Empty;

        [Column("customer_category")]
        public CustomerCategory Category { get; set; } = CustomerCategory.Regular;

        [Column("loyalty_points")]
        public int LoyaltyPoints { get; set; } = 0;

        [Column("total_spent")]
        public decimal TotalSpent { get; set; } = 0;

        [Column("visit_count")]
        public int VisitCount { get; set; } = 0;

        [Column("last_visit")]
        public DateTime? LastVisit { get; set; }

        [Column("notes")]
        public string Notes { get; set; } = string.Empty;

        [Column("is_vip")]
        public bool IsVip { get; set; } = false;

        [Column("discount_percentage")]
        public decimal DiscountPercentage { get; set; } = 0;

        [Column("birth_date")]
        public DateTime? BirthDate { get; set; }

        [Column("preferred_payment_method")]
        public CustomerPaymentMethod PreferredPaymentMethod { get; set; } = CustomerPaymentMethod.Cash;

        // Navigation properties
        public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
        public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();
        // public virtual ICollection<CustomerDiscount> CustomerDiscounts { get; set; } = new List<CustomerDiscount>();
    }

    public enum CustomerCategory
    {
        Regular = 0,
        VIP = 1,
        Premium = 2,
        Corporate = 3,
        Student = 4,
        Senior = 5
    }

    public enum CustomerPaymentMethod
    {
        Cash = 0,
        Card = 1,
        Voucher = 2,
        Mobile = 3
    }
}
