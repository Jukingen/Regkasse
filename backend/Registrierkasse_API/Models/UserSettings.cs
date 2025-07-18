using System.ComponentModel.DataAnnotations;

namespace Registrierkasse_API.Models
{
    public class UserSettings : BaseEntity
    {
        [Required]
        public string UserId { get; set; } = string.Empty;
        public string? Language { get; set; }
        public string? Theme { get; set; }
        public virtual ApplicationUser User { get; set; } = null!;
    }
} 
