namespace KasseAPI_Final.DTOs;

public sealed class ProductPriceHistoryItemDto
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public decimal OldPrice { get; set; }

    public decimal NewPrice { get; set; }

    public Guid OldTaxGroupId { get; set; }

    public string? OldTaxGroupName { get; set; }

    public Guid NewTaxGroupId { get; set; }

    public string? NewTaxGroupName { get; set; }

    public decimal OldTaxRate { get; set; }

    public decimal NewTaxRate { get; set; }

    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    public bool IsActive { get; set; }

    public Guid ChangedBy { get; set; }

    public string Reason { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public bool IsRksvCompliant { get; set; }

    public string? RksvNote { get; set; }

    public DateTime? RksvVerifiedAt { get; set; }
}

public sealed class ProductPriceVersionItemDto
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public Guid TaxGroupId { get; set; }

    public string? TaxGroupName { get; set; }

    public DateTime ValidFrom { get; set; }

    public DateTime? ValidTo { get; set; }

    public bool IsCurrent { get; set; }

    public string Version { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
