using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse.Models
{
    public abstract class BaseEntity
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
        
        [Column("created_by")]
        [MaxLength(450)]
        public string? CreatedBy { get; set; }
        
        [Column("updated_by")]
        [MaxLength(450)]
        public string? UpdatedBy { get; set; }
        
        [Column("is_active")]
        public bool IsActive { get; set; } = true;
    }
} 