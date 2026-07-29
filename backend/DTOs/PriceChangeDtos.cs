using KasseAPI_Final.Models;

namespace KasseAPI_Final.DTOs;

public sealed class PriceChangeRequest
{
    public Guid TenantId { get; set; }

    public Guid ProductId { get; set; }

    public decimal NewPrice { get; set; }

    public Guid NewTaxGroupId { get; set; }

    public Guid ChangedBy { get; set; }

    /// <summary>Actor role for audit (e.g. Manager). Optional.</summary>
    public string? ChangedByRole { get; set; }

    public string? Reason { get; set; }

    /// <summary>
    /// When true, updates price in place even if the product has fiscal sales history.
    /// Default false: RKSV path creates a new catalog product version instead.
    /// </summary>
    public bool ForceInPlaceUpdate { get; set; }
}

public sealed class PriceChangeValidationResult
{
    public bool IsValid { get; init; }

    public bool HasWarning { get; init; }

    public string? ErrorMessage { get; init; }

    public string? WarningMessage { get; init; }

    /// <summary>True when the product already appears on order lines (fiscal sales trail).</summary>
    public bool HasFiscalHistory { get; init; }

    /// <summary>True when ChangePriceAsync will create a new catalog product row.</summary>
    public bool RequiresNewProductVersion { get; init; }

    /// <summary>Detailed RKSV findings (warnings / errors / requirements).</summary>
    public RksvPriceChangeComplianceResult? Compliance { get; init; }

    public static PriceChangeValidationResult Success(
        string? warning = null,
        bool hasFiscalHistory = false,
        bool requiresNewProductVersion = false,
        RksvPriceChangeComplianceResult? compliance = null) =>
        new()
        {
            IsValid = true,
            HasWarning = !string.IsNullOrWhiteSpace(warning),
            WarningMessage = warning,
            HasFiscalHistory = hasFiscalHistory,
            RequiresNewProductVersion = requiresNewProductVersion,
            Compliance = compliance,
        };

    public static PriceChangeValidationResult Fail(
        string errorMessage,
        RksvPriceChangeComplianceResult? compliance = null) =>
        new()
        {
            IsValid = false,
            ErrorMessage = errorMessage,
            HasFiscalHistory = compliance?.HasFiscalHistory ?? false,
            RequiresNewProductVersion = compliance?.RequiresNewProductVersion ?? false,
            Compliance = compliance,
        };

    public static PriceChangeValidationResult Warn(
        string warningMessage,
        bool hasFiscalHistory = true,
        bool requiresNewProductVersion = true,
        RksvPriceChangeComplianceResult? compliance = null) =>
        new()
        {
            IsValid = true,
            HasWarning = true,
            WarningMessage = warningMessage,
            HasFiscalHistory = hasFiscalHistory,
            RequiresNewProductVersion = requiresNewProductVersion,
            Compliance = compliance,
        };
}

public sealed class PriceChangeResult
{
    public bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }

    public string? WarningMessage { get; init; }

    public Guid? ProductId { get; init; }

    /// <summary>Archived predecessor product id when a new catalog version was created.</summary>
    public Guid? ArchivedProductId { get; init; }

    public Guid? PriceVersionId { get; init; }

    public string? Version { get; init; }

    /// <summary>Catalog product version number (products.version).</summary>
    public int? CatalogVersion { get; init; }

    public bool CreatedNewProductVersion { get; init; }

    public decimal? OldPrice { get; init; }

    public decimal? NewPrice { get; init; }

    public Guid? OldTaxGroupId { get; init; }

    public Guid? NewTaxGroupId { get; init; }

    public decimal? OldTaxRate { get; init; }

    public decimal? NewTaxRate { get; init; }

    public static PriceChangeResult Fail(string errorMessage) =>
        new() { Succeeded = false, ErrorMessage = errorMessage };

    public static PriceChangeResult Success(
        Guid productId,
        Guid priceVersionId,
        string? version,
        decimal oldPrice,
        decimal newPrice,
        Guid oldTaxGroupId,
        Guid newTaxGroupId,
        decimal oldTaxRate,
        decimal newTaxRate,
        string? warningMessage = null,
        bool createdNewProductVersion = false,
        Guid? archivedProductId = null,
        int? catalogVersion = null) =>
        new()
        {
            Succeeded = true,
            ProductId = productId,
            PriceVersionId = priceVersionId,
            Version = version,
            OldPrice = oldPrice,
            NewPrice = newPrice,
            OldTaxGroupId = oldTaxGroupId,
            NewTaxGroupId = newTaxGroupId,
            OldTaxRate = oldTaxRate,
            NewTaxRate = newTaxRate,
            WarningMessage = warningMessage,
            CreatedNewProductVersion = createdNewProductVersion,
            ArchivedProductId = archivedProductId,
            CatalogVersion = catalogVersion,
        };
}
