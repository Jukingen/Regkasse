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

        /// <summary>VAT oranı yüzde olarak (örn. 10, 20). Hesaplamada fraction = VatRate/100 kullanılır.</summary>
        [Column("vat_rate", TypeName = "decimal(5,2)")]
        [Range(0, 100, ErrorMessage = "VAT rate must be between 0 and 100")]
        public decimal VatRate { get; set; } = 20m;

        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
