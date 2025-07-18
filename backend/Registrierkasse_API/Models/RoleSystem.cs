using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse_API.Models
{
    /// <summary>
    /// Kullanıcı rolleri - Admin ve Kasiyer için temel roller
    /// </summary>
    public class Role
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;
        
        [StringLength(200)]
        public string Description { get; set; } = string.Empty;
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? UpdatedAt { get; set; }
        
        // Navigation properties
        public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
        public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }

    /// <summary>
    /// Sistem yetkileri - Her işlem için ayrı yetki tanımı
    /// </summary>
    public class Permission
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [StringLength(200)]
        public string Description { get; set; } = string.Empty;
        
        [Required]
        [StringLength(50)]
        public string Resource { get; set; } = string.Empty; // users, products, sales, receipts, etc.
        
        [Required]
        [StringLength(50)]
        public string Action { get; set; } = string.Empty; // create, read, update, delete, export
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }

    /// <summary>
    /// Kullanıcı-Rol ilişkisi - Many-to-many bağlantı
    /// </summary>
    public class UserRole
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string UserId { get; set; } = string.Empty;
        
        [Required]
        public int RoleId { get; set; }
        
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
        
        public string AssignedBy { get; set; } = string.Empty;
        
        public bool IsActive { get; set; } = true;
        
        // Navigation properties
        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; } = null!;
        
        [ForeignKey("RoleId")]
        public virtual Role Role { get; set; } = null!;
    }

    /// <summary>
    /// Rol-Yetki ilişkisi - Many-to-many bağlantı
    /// </summary>
    public class RolePermission
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int RoleId { get; set; }
        
        [Required]
        public int PermissionId { get; set; }
        
        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
        
        public string GrantedBy { get; set; } = string.Empty;
        
        public bool IsActive { get; set; } = true;
        
        // Navigation properties
        [ForeignKey("RoleId")]
        public virtual Role Role { get; set; } = null!;
        
        [ForeignKey("PermissionId")]
        public virtual Permission Permission { get; set; } = null!;
    }

    /// <summary>
    /// Demo kullanıcı aktivite logları - Test ve denetim için
    /// </summary>
    public class DemoUserLog
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Username { get; set; } = string.Empty;
        
        [Required]
        [StringLength(100)]
        public string Action { get; set; } = string.Empty;
        
        [StringLength(500)]
        public string Details { get; set; } = string.Empty;
        
        [StringLength(45)]
        public string IpAddress { get; set; } = string.Empty;
        
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        [StringLength(20)]
        public string UserType { get; set; } = string.Empty; // Cashier, Admin
    }

    /// <summary>
    /// Detaylı işlem logları - Before/After değişiklik kayıtları
    /// </summary>
    public class OperationLog
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Operation { get; set; } = string.Empty; // create, update, delete
        
        [StringLength(500)]
        public string Details { get; set; } = string.Empty; // İşlem detayları
        
        [StringLength(100)]
        public string UserId { get; set; } = string.Empty;
        
        [StringLength(45)]
        public string IpAddress { get; set; } = string.Empty;
        
        [StringLength(500)]
        public string UserAgent { get; set; } = string.Empty; // Browser/Client bilgisi
        
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }
    }
} 