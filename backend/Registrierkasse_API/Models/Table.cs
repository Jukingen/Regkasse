using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse.Models
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
        
        public virtual ICollection<TableReservation> Reservations { get; set; } = new List<TableReservation>();
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