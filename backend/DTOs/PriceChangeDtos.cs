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
}

public sealed class PriceChangeValidationResult
{
    public bool IsValid { get; init; }

    public bool HasWarning { get; init; }

    public string? ErrorMessage { get; init; }

    public string? WarningMessage { get; init; }

    /// <summary>True when the product already appears on order lines (fiscal sales trail).</summary>
    public bool HasFiscalHistory { get; init; }

    public static PriceChangeValidationResult Success(string? warning = null, bool hasFiscalHistory = false) =>
        new()
        {
            IsValid = true,
            HasWarning = !string.IsNullOrWhiteSpace(warning),
            WarningMessage = warning,
            HasFiscalHistory = hasFiscalHistory,
        };

    public static PriceChangeValidationResult Fail(string errorMessage) =>
        new()
        {
            IsValid = false,
            ErrorMessage = errorMessage,
        };

    public static PriceChangeValidationResult Warn(string warningMessage, bool hasFiscalHistory = true) =>
        new()
        {
            IsValid = true,
            HasWarning = true,
            WarningMessage = warningMessage,
            HasFiscalHistory = hasFiscalHistory,
        };
}

public sealed class PriceChangeResult
{
    public bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }

    public string? WarningMessage { get; init; }

    public Guid? ProductId { get; init; }

    public Guid? PriceVersionId { get; init; }

    public string? Version { get; init; }

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
        string? warningMessage = null) =>
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
        };
}
