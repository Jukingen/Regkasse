using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("users")]
    public class User : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(100)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(100)]
        public string Username { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(100)]
        public string Role { get; set; } = "Cashier"; // Admin, Cashier, Demo
        
        [MaxLength(500)]
        public string? PasswordHash { get; set; }
        

        
        public DateTime? LastLoginAt { get; set; }
        
        public string? Language { get; set; } = "de-DE"; // de-DE, en, tr
        
        // Navigation properties
        public virtual UserSettings? UserSettings { get; set; }
        public virtual ICollection<PaymentDetails> CreatedPayments { get; set; } = new List<PaymentDetails>();
        public virtual ICollection<PaymentDetails> UpdatedPayments { get; set; } = new List<PaymentDetails>();
    }
}
