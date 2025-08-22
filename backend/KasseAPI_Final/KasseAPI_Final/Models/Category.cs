using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("categories")]
    public class Category : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(20)]
        public string? Color { get; set; }

        [MaxLength(50)]
        public string? Icon { get; set; }

        public int SortOrder { get; set; } = 0;

        // Navigation properties - Geçici olarak kapatıldı EF shadow property sorununu çözmek için
        // public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
