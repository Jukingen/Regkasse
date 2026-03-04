using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("receipt_items")]
    public class ReceiptItem
    {
        [Key]
        [Column("item_id")]
        public Guid ItemId { get; set; } = Guid.NewGuid();

        [Required]
        [Column("receipt_id")]
        public Guid ReceiptId { get; set; }

        [Required]
        [Column("product_name")]
        [StringLength(255)]
        public string ProductName { get; set; } = string.Empty;

        [Required]
        [Column("quantity")]
        public int Quantity { get; set; }

        [Required]
        [Column("unit_price", TypeName = "decimal(10, 2)")]
        public decimal UnitPrice { get; set; }

        [Required]
        [Column("total_price", TypeName = "decimal(10, 2)")]
        public decimal TotalPrice { get; set; }

        /// <summary>Satır net tutarı (deterministik fiş toplamı için).</summary>
        [Required]
        [Column("line_net", TypeName = "decimal(10, 2)")]
        public decimal LineNet { get; set; }

        /// <summary>Satır vergi tutarı (deterministik fiş toplamı için).</summary>
        [Required]
        [Column("vat_amount", TypeName = "decimal(10, 2)")]
        public decimal VatAmount { get; set; }

        [Required]
        [Column("tax_rate", TypeName = "decimal(5, 2)")]
        public decimal TaxRate { get; set; }

        /// <summary>Varsa: bu satır bir modifier (Extra Zutaten); parent ana ürün satırının ItemId'si.</summary>
        [Column("parent_item_id")]
        public Guid? ParentItemId { get; set; }

        /// <summary>Kategori adı (gösterim için, opsiyonel).</summary>
        [Column("category_name")]
        [StringLength(100)]
        public string? CategoryName { get; set; }

        // Navigation Property
        [ForeignKey("ReceiptId")]
        public virtual Receipt? Receipt { get; set; }
    }
}
