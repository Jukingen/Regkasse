using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs
{
    /// <summary>
    /// Modifier group list/detail response (Admin + POS). Faz 1: Products = suggested add-on product refs (fiyat Product'ta); Modifiers = legacy.
    /// </summary>
    public class ModifierGroupDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int MinSelections { get; set; }
        public int? MaxSelections { get; set; }
        public bool IsRequired { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        /// <summary>Legacy. Yeni client Products kullanır. Fiyat ProductModifier'dan.</summary>
        public List<ModifierDto> Modifiers { get; set; } = new();
        /// <summary>Faz 1: Bu grupta önerilen product'lar (sellable add-on). Fiyat/vergi Product'tan; JSON: products (camelCase).</summary>
        public List<AddOnGroupProductItemDto> Products { get; set; } = new();
    }

    /// <summary>
    /// Faz 1: Grup içi product referansı (suggested add-on). Fiyat Product'ta; response'ta Product'tan doldurulur.
    /// JSON: productId, productName, price, taxType, sortOrder (camelCase).
    /// </summary>
    public class AddOnGroupProductItemDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int TaxType { get; set; }
        public int SortOrder { get; set; }
    }

    /// <summary>
    /// Single modifier (name, price, tax). Legacy; yeni akış AddOnGroupProductItemDto / Products kullanır.
    /// </summary>
    public class ModifierDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int TaxType { get; set; }
        public int SortOrder { get; set; }
    }

    /// <summary>
    /// Create or update modifier group request.
    /// </summary>
    public class CreateModifierGroupRequest
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        public int MinSelections { get; set; }
        public int? MaxSelections { get; set; }
        public bool IsRequired { get; set; }
        public int SortOrder { get; set; }
    }

    /// <summary>
    /// Add modifier to a group. Legacy; yeni akış AddProductToGroupRequest kullanır.
    /// </summary>
    public class CreateModifierRequest
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int TaxType { get; set; } = 1;
        public int SortOrder { get; set; }
    }

    /// <summary>
    /// Faz 1: Gruba product ekleme. ProductId (mevcut product) veya CreateNewAddOnProduct (yeni Zusatzprodukt) – en az biri dolu olmalı.
    /// JSON: productId (optional), createNewAddOnProduct (optional).
    /// </summary>
    public class AddProductToGroupRequest
    {
        /// <summary>Mevcut product (IsSellableAddOn veya uygun kategori). ProductId verilirse createNewAddOnProduct verilmez.</summary>
        public Guid? ProductId { get; set; }
        /// <summary>Yeni add-on product oluşturup gruba ekle. ProductId verilmezse kullanılır.</summary>
        public CreateNewAddOnProductRequest? CreateNewAddOnProduct { get; set; }
    }

    /// <summary>
    /// Yeni Zusatzprodukt oluşturma (gruba eklenir). Name zorunlu; diğerleri varsayılanlı.
    /// </summary>
    public class CreateNewAddOnProductRequest
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int TaxType { get; set; } = 1;
        public Guid? CategoryId { get; set; }
        public int SortOrder { get; set; }
    }

    /// <summary>
    /// Assign modifier groups to a product.
    /// </summary>
    public class SetProductModifierGroupsRequest
    {
        public List<Guid> ModifierGroupIds { get; set; } = new();
    }

    /// <summary>
    /// Satır bazında seçili modifier (cart item / table-order item). JSON: camelCase.
    /// </summary>
    public class SelectedModifierDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; } = 1;
        public Guid? GroupId { get; set; }
    }

    /// <summary>
    /// add-item / update-item request: FE gönderir; name/price DB'den türetilir (güvenlik).
    /// Quantity &lt; 1 ise 1 kabul edilir (backward compat). Aynı id iki kez gelirse miktarlar toplanır.
    /// </summary>
    public class SelectedModifierInputDto
    {
        public Guid Id { get; set; }
        /// <summary>Miktar; yoksa veya &lt; 1 ise 1 kullanılır.</summary>
        public int Quantity { get; set; } = 1;
    }
}
