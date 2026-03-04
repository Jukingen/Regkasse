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
}
