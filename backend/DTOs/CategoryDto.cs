using KasseAPI_Final.Models;

namespace KasseAPI_Final.DTOs;

/// <summary>Admin category list/detail response. Key is immutable; Name is user-editable.</summary>
public class CategoryDto
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public int SortOrder { get; set; }
    public int ProductCount { get; set; }
    public decimal DefaultTaxRate { get; set; }
    public RksvProductCategory FiscalCategory { get; set; }
    public bool IsSystemCategory { get; set; }
    public string? OriginalDemoName { get; set; }
    public bool IsActive { get; set; }
}
