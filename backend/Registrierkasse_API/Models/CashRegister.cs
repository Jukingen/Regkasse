using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse_API.Models
{
    public enum RegisterStatus
    {
        Open,
        Active,
        Inactive,
        Maintenance,
        Closed
    }

    [Table("cash_registers")]
    public class CashRegister : BaseEntity
    {
        [Required]
        [Column("register_number")]
        [MaxLength(20)]
        public string RegisterNumber { get; set; } = string.Empty;
        
        [Required]
        [Column("tse_id")]
        [MaxLength(50)]
        public string TseId { get; set; } = string.Empty;
        
        [Required]
        [Column("kassen_id")]
        [MaxLength(50)]
        public string KassenId { get; set; } = string.Empty;

        [Required]
        [Column("location")]
        [MaxLength(100)]
        public string Location { get; set; } = string.Empty;

        [Column("starting_balance")]
        public decimal StartingBalance { get; set; }

        [Column("current_balance")]
        public decimal CurrentBalance { get; set; }

        [Column("last_balance_update")]
        public DateTime? LastBalanceUpdate { get; set; }

        [Column("status")]
        [MaxLength(20)]
        public RegisterStatus Status { get; set; } = RegisterStatus.Active;
        
        [Column("current_user_id")]
        public string? CurrentUserId { get; set; }
        
        [ForeignKey("CurrentUserId")]
        public virtual ApplicationUser? CurrentUser { get; set; }
        
        [Column("last_closing_date")]
        public DateTime? LastClosingDate { get; set; }
        
        [Column("last_closing_amount")]
        public decimal LastClosingAmount { get; set; }
        
        [Column("notes")]
        [MaxLength(500)]
        public string Notes { get; set; } = string.Empty;
        
        [Column("is_active")]
        public new bool IsActive { get; set; } = true;
        
        // Navigation properties
        public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
        public virtual ICollection<CashRegisterTransaction> Transactions { get; set; } = new List<CashRegisterTransaction>();
    }
} 
