using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs;

public sealed class TaxRegulationDto
{
    public DateTime EffectiveDate { get; set; }

    public decimal StandardRate { get; set; }

    public decimal ReducedRate { get; set; }

    public decimal ReducedNewRate { get; set; }

    public decimal MiddleRate { get; set; }

    public decimal ZeroRate { get; set; }

    public bool IsActive { get; set; }

    public string Description { get; set; } = string.Empty;

    public IReadOnlyList<decimal> AllowedRates { get; set; } = [];
}

public sealed class TaxChangeImpactDto
{
    public Guid TenantId { get; set; }

    public decimal OldRate { get; set; }

    public decimal NewRate { get; set; }

    public int AffectedProductCount { get; set; }

    public decimal AffectedCatalogValue { get; set; }

    public decimal EstimatedVatDelta { get; set; }
}

public sealed class TaxRateValidationRequest
{
    [Range(0, 100)]
    public decimal Rate { get; set; }
}

public sealed class TaxRateValidationResponse
{
    public decimal Rate { get; set; }

    public bool IsValid { get; set; }
}

public sealed class TaxChangeImpactRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Range(0, 100)]
    public decimal OldRate { get; set; }

    [Range(0, 100)]
    public decimal NewRate { get; set; }
}
