using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace Registrierkasse.Models
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
        
        public virtual ICollection<CashRegister> AssignedCashRegisters { get; set; } = new List<CashRegister>();
        
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
        
        public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
        
        public virtual UserSettings? Settings { get; set; }
        
        public virtual ICollection<CashRegisterTransaction> Transactions { get; set; } = new List<CashRegisterTransaction>();
        
        public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
    }
} 