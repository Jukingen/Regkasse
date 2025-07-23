using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse_API.Models
{
    [Table("product_options")]
    public class ProductOption : BaseEntity
    {
        [Required]
        [Column("product_id")]
        public Guid ProductId { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty; // Örn: "Ekstra Peynir", "Sos Seçimi", "Pişirme Derecesi"

        [Column("description")]
        [MaxLength(300)]
        public string? Description { get; set; }

        [Required]
        [Column("option_type")]
        public OptionType OptionType { get; set; } // Tek seçim, çoklu seçim, metin girişi

        [Column("is_required")]
        public bool IsRequired { get; set; } = false; // Zorunlu seçenek mi?

        [Column("max_selections")]
        public int MaxSelections { get; set; } = 1; // Maksimum seçim sayısı

        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public virtual Product Product { get; set; } = null!;
        public virtual ICollection<ProductOptionValue> OptionValues { get; set; } = new List<ProductOptionValue>();
    }

    public enum OptionType
    {
        SingleChoice = 1,    // Tek seçim (radio button)
        MultipleChoice = 2,  // Çoklu seçim (checkbox)
        TextInput = 3,       // Metin girişi
        NumberInput = 4      // Sayı girişi
    }

    [Table("product_option_values")]
    public class ProductOptionValue : BaseEntity
    {
        [Required]
        [Column("option_id")]
        public Guid OptionId { get; set; }

        [Required]
        [Column("value")]
        [MaxLength(100)]
        public string Value { get; set; } = string.Empty; // Örn: "Az Pişmiş", "Orta", "İyi Pişmiş"

        [Column("price_modifier")]
        public decimal PriceModifier { get; set; } = 0; // Ek fiyat

        [Column("is_default")]
        public bool IsDefault { get; set; } = false;

        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public virtual ProductOption Option { get; set; } = null!;
    }
} 