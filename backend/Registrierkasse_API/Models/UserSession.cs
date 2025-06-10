using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse.Models
{
    [Table("user_sessions")]
    public class UserSession
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("user_id")]
        public string UserId { get; set; }

        [Required]
        [Column("session_id")]
        [MaxLength(100)]
        public string SessionId { get; set; }

        [Required]
        [Column("device_info")]
        [MaxLength(500)]
        public string DeviceInfo { get; set; }

        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Required]
        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; }

        [Required]
        [Column("last_activity")]
        public DateTime LastActivity { get; set; }

        [Required]
        [Column("is_active")]
        public bool IsActive { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }
    }
} 