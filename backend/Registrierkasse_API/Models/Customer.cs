using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse.Models
{
    [Table("customers")]
    public class Customer : BaseEntity
    {
        [Required]
        [Column("customer_number")]
        [MaxLength(20)]
        public string CustomerNumber { get; set; } = string.Empty;
        
        [Required]
        [Column("first_name")]
        [MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;
        
        [Required]
        [Column("last_name")]
        [MaxLength(50)]
        public string LastName { get; set; } = string.Empty;
        
        [Column("email")]
        [MaxLength(100)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        
        [Column("phone")]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;
        
        [Column("address")]
        [MaxLength(200)]
        public string Address { get; set; } = string.Empty;
        
        [Column("city")]
        [MaxLength(50)]
        public string City { get; set; } = string.Empty;
        
        [Column("postal_code")]
        [MaxLength(10)]
        public string PostalCode { get; set; } = string.Empty;
        
        [Column("country")]
        [MaxLength(50)]
        public string Country { get; set; } = string.Empty;
        
        [Column("tax_number")]
        [MaxLength(20)]
        public string TaxNumber { get; set; } = string.Empty;
        
        [Column("company_name")]
        [MaxLength(100)]
        public string CompanyName { get; set; } = string.Empty;
        
        [NotMapped]
        public string Name => !string.IsNullOrEmpty(CompanyName) ? CompanyName : $"{FirstName} {LastName}".Trim();
        
        [Column("is_active")]
        public new bool IsActive { get; set; } = true;
        
        [Column("notes")]
        [MaxLength(500)]
        public string Notes { get; set; } = string.Empty;
        
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
        
        public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    }

    public enum CustomerType
    {
        Individual,
        Business
    }
} 