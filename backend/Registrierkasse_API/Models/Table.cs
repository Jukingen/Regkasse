using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse_API.Models
{
    [Table("tables")]
    public class Table : BaseEntity
    {
        [Required]
        [Column("number")]
        public int Number { get; set; }
        
        [Required]
        [Column("name")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        [Column("capacity")]
        public int Capacity { get; set; }
        
        [Required]
        [Column("location")]
        [MaxLength(100)]
        public string Location { get; set; } = string.Empty;
        
        [Column("is_active")]
        public new bool IsActive { get; set; } = true;
        
        [Column("current_order_id")]
        public Guid? CurrentOrderId { get; set; }
        
        [ForeignKey("CurrentOrderId")]
        public virtual Order? CurrentOrder { get; set; }
        
        // Masa-cart ilişkisi için yeni alanlar
        [Column("current_cart_id")]
        public string? CurrentCartId { get; set; }
        
        [ForeignKey("CurrentCartId")]
        public virtual Cart? CurrentCart { get; set; }
        
        [Column("status")]
        [MaxLength(20)]
        public string Status { get; set; } = "empty"; // empty, occupied, reserved, paid
        
        [Column("customer_name")]
        [MaxLength(100)]
        public string CustomerName { get; set; } = string.Empty;
        
        [Column("start_time")]
        public DateTime? StartTime { get; set; }
        
        [Column("last_order_time")]
        public DateTime? LastOrderTime { get; set; }
        
        [Column("total_paid")]
        public decimal TotalPaid { get; set; } = 0;
        
        [Column("current_total")]
        public decimal CurrentTotal { get; set; } = 0;
        
        // Masa özel ayarları
        [Column("service_charge_percentage")]
        public decimal ServiceChargePercentage { get; set; } = 0; // Servis ücreti yüzdesi
        
        [Column("tip_percentage")]
        public decimal TipPercentage { get; set; } = 0; // Bahşiş yüzdesi
        
        [Column("split_bill_enabled")]
        public bool SplitBillEnabled { get; set; } = true; // Bölünmüş hesap aktif mi?
        
        [Column("max_split_count")]
        public int MaxSplitCount { get; set; } = 4; // Maksimum bölünme sayısı
        
        [Column("notes")]
        [MaxLength(500)]
        public string Notes { get; set; } = string.Empty;
        
        public virtual ICollection<TableReservation> Reservations { get; set; } = new List<TableReservation>();
        
        public virtual ICollection<Order> OrderHistory { get; set; } = new List<Order>();
        
        public virtual ICollection<Cart> CartHistory { get; set; } = new List<Cart>();
    }
    
    [Table("table_reservations")]
    public class TableReservation : BaseEntity
    {
        [Required]
        [Column("table_id")]
        public Guid TableId { get; set; }
        
        [ForeignKey("TableId")]
        public virtual Table Table { get; set; } = null!;
        
        [Required]
        [Column("customer_name")]
        [MaxLength(100)]
        public string CustomerName { get; set; } = string.Empty;
        
        [Required]
        [Column("customer_phone")]
        [MaxLength(20)]
        public string CustomerPhone { get; set; } = string.Empty;
        
        [Required]
        [Column("guest_count")]
        public int GuestCount { get; set; }
        
        [Required]
        [Column("reservation_time")]
        public DateTime ReservationTime { get; set; }
        
        [Column("notes")]
        [MaxLength(500)]
        public string Notes { get; set; } = string.Empty;
        
        [Required]
        [Column("status")]
        public ReservationStatus Status { get; set; } = ReservationStatus.Confirmed;
    }
    
    public enum ReservationStatus
    {
        Confirmed,
        Cancelled,
        Completed,
        NoShow
    }
} 
