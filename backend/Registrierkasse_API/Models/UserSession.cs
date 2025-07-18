using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse_API.Models
{
    public class UserSession : BaseEntity
    {
        [Required]
        public string UserId { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public string DeviceInfo { get; set; } = string.Empty;
        public new DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; }
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
        public virtual ApplicationUser User { get; set; } = null!;
    }
} 
