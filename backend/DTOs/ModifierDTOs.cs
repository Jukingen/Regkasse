using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs
{
    /// <summary>
    /// Modifier group list/detail response (Admin + POS).
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
        public List<ModifierDto> Modifiers { get; set; } = new();
    }

    /// <summary>
    /// Single modifier (name, price, tax).
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
    /// Add modifier to a group.
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
