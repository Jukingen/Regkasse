namespace KasseAPI_Final.DTOs;

public sealed class TaxBulkUpdateRequest
{
    public Guid OldTaxGroupId { get; set; }

    public Guid NewTaxGroupId { get; set; }

    public string? Reason { get; set; }
}

public sealed class TaxBulkUpdateResultDto
{
    public int TotalProducts { get; set; }

    public int UpdatedProducts { get; set; }

    public decimal OldRate { get; set; }

    public decimal NewRate { get; set; }

    public Guid OldTaxGroupId { get; set; }

    public Guid NewTaxGroupId { get; set; }
}

/// <summary>Assign one tax group to an explicit product id list (quick actions / selection).</summary>
public sealed class TaxApplyToProductsRequest
{
    public Guid TaxGroupId { get; set; }

    public List<Guid> ProductIds { get; set; } = [];

    public string? Reason { get; set; }
}

public sealed class TaxApplyToProductsResultDto
{
    public int RequestedCount { get; set; }

    public int UpdatedProducts { get; set; }

    public int UnchangedProducts { get; set; }

    public int NotFound { get; set; }

    public Guid TaxGroupId { get; set; }

    public decimal NewRate { get; set; }
}
