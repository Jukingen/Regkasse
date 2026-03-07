using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs
{
    /// <summary>
    /// Modifier group list/detail response (Admin + POS). Primary: Products (sellable add-ons). Legacy: Modifiers (read-only for existing data).
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
        /// <summary>Primary: Suggested add-on products for this group. Price/tax from Product. Use this for new flows.</summary>
        public List<AddOnGroupProductItemDto> Products { get; set; } = new();
        /// <summary>Phase 2 legacy: Read-only for existing groups. Prefer Products. Will be removed after migration.</summary>
        [Obsolete("Prefer Products for add-ons. Modifiers kept for read compatibility only.", false)]
        public List<ModifierDto> Modifiers { get; set; } = new();
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
        /// <summary>Admin: false = migriert/deaktiviert, in UI als „migriert“ anzeigen.</summary>
        public bool IsActive { get; set; } = true;
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
    /// Phase 2: Creation frozen; endpoint returns 410. Do not use for new code. TODO Phase 2: Remove after migration.
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
    /// Phase 2 deprecated: Satır bazında seçili modifier (cart/table-order). Read-only in API response for legacy carts. New flow uses flat cart lines (add-on = separate product line).
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
    /// Phase 2 deprecated: add-item/update-item legacy modifier input. New add-ons should be added as separate add-item(productId) lines. Still accepted for backward compat; prefer flat cart.
    /// </summary>
    public class SelectedModifierInputDto
    {
        public Guid Id { get; set; }
        /// <summary>Miktar; yoksa veya &lt; 1 ise 1 kullanılır.</summary>
        public int Quantity { get; set; } = 1;
    }
}
