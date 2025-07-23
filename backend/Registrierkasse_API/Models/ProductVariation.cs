using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse_API.Models
{
    [Table("product_variations")]
    public class ProductVariation : BaseEntity
    {
        [Required]
        [Column("product_id")]
        public Guid ProductId { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty; // Örn: "Küçük", "Orta", "Büyük", "Tek Porsiyon", "Çift Porsiyon"

        [Column("description")]
        [MaxLength(300)]
        public string? Description { get; set; }

        [Required]
        [Column("price_modifier")]
        public decimal PriceModifier { get; set; } // Ana fiyata eklenecek/çıkarılacak tutar

        [Column("price_multiplier")]
        public decimal PriceMultiplier { get; set; } = 1.0m; // Fiyat çarpanı (örn: 1.5 = %50 artış)

        [Column("is_default")]
        public bool IsDefault { get; set; } = false; // Varsayılan seçenek

        [Column("sort_order")]
        public int SortOrder { get; set; } = 0; // Sıralama

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("icon")]
        [MaxLength(50)]
        public string? Icon { get; set; } // UI için ikon

        [Column("color")]
        [MaxLength(20)]
        public string? Color { get; set; } // UI için renk

        // Navigation properties
        public virtual Product Product { get; set; } = null!;
    }
} 