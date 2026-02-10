using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace KasseAPI_Final.Models
{
    [Table("users")]
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [Column("first_name")]
        [MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;
        
        [Required]
        [Column("last_name")]
        [MaxLength(50)]
        public string LastName { get; set; } = string.Empty;
        
        // Computed property
        public string Name => $"{FirstName} {LastName}".Trim();
        
        [Column("employee_number")]
        [MaxLength(20)]
        public string EmployeeNumber { get; set; } = string.Empty;
        
        [Column("role")]
        [MaxLength(20)]
        public string Role { get; set; } = "Cashier";
        
        [Column("tax_number")]
        [MaxLength(20)]
        public string TaxNumber { get; set; } = string.Empty;
        
        [Column("notes")]
        [MaxLength(500)]
        public string Notes { get; set; } = string.Empty;
        
        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        
        [Column("last_login")]
        public DateTime? LastLogin { get; set; }
        
        // Demo kullanıcı alanları
        [Column("account_type")]
        [MaxLength(20)]
        public string AccountType { get; set; } = "real"; // "real", "demo"
        
        [Column("is_demo")]
        public bool IsDemo { get; set; } = false;
        
        [Column("last_login_at")]
        public DateTime? LastLoginAt { get; set; }
        
        [Column("login_count")]
        public int LoginCount { get; set; } = 0;
        
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
        
        // Navigation properties
        public virtual ICollection<CashRegister> CashRegisters { get; set; } = new List<CashRegister>();
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
        public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();
        public virtual ICollection<CashRegisterTransaction> Transactions { get; set; } = new List<CashRegisterTransaction>();
        public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
        // public virtual UserSettings? Settings { get; set; }
        // public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}
