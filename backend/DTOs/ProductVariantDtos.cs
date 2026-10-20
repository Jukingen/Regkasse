using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs;

public static class ProductVariantSaleCodes
{
    public const string Required = "VARIANT_REQUIRED";
    public const string Invalid = "VARIANT_INVALID";
    public const string NotAllowed = "VARIANT_NOT_ALLOWED";
    public const string StockInsufficient = "STOCK_INSUFFICIENT";
}

public sealed class ProductVariantDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string VariantLabel { get; set; } = string.Empty;
    public decimal PriceDelta { get; set; }
    public int Stock { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreateProductVariantRequest
{
    [Required]
    [MaxLength(64)]
    public string Sku { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string VariantLabel { get; set; } = string.Empty;

    public decimal PriceDelta { get; set; }

    public int Stock { get; set; }
}

public sealed class UpdateProductVariantRequest
{
    [Required]
    [MaxLength(64)]
    public string Sku { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string VariantLabel { get; set; } = string.Empty;

    public decimal PriceDelta { get; set; }

    public int Stock { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class AdjustProductVariantStockRequest
{
    public int Delta { get; set; }
}

public sealed class ProductVariantErrorDto
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public abstract record ProductVariantMutation
{
    public sealed record Ok(ProductVariantDto Variant) : ProductVariantMutation;

    public sealed record NotFound : ProductVariantMutation;

    public sealed record Error(int StatusCode, string Code, string Message) : ProductVariantMutation;
}
