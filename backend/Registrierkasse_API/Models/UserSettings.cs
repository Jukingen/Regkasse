using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse.Models
{
    [Table("user_settings")]
    public class UserSettings : BaseEntity
    {
        [Required]
        [Column("user_id")]
        public string UserId { get; set; } = string.Empty;
        
        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; } = null!;
        
        [Column("language")]
        [MaxLength(10)]
        public string Language { get; set; } = "de-DE";
        
        [Column("theme")]
        [MaxLength(20)]
        public string Theme { get; set; } = "light";
        
        [Column("notifications_enabled")]
        public bool NotificationsEnabled { get; set; } = true;
        
        [Column("email_notifications")]
        public bool EmailNotifications { get; set; } = true;
        
        [Column("printer_name")]
        [MaxLength(100)]
        public string PrinterName { get; set; } = string.Empty;
        
        [Column("default_payment_method")]
        [MaxLength(20)]
        public string DefaultPaymentMethod { get; set; } = "cash";
        
        [Column("notes")]
        [MaxLength(500)]
        public string Notes { get; set; } = string.Empty;
        
        [Column("is_active")]
        public new bool IsActive { get; set; } = true;
    }
} 