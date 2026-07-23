using System.ComponentModel.DataAnnotations;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.DTOs;

public sealed class ProductPriceUpdateDto
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal NewPrice { get; set; }
}

/// <summary>API request for <c>POST /api/admin/impact-simulation/simulate</c>.</summary>
public sealed class ImpactSimulationRequestDto
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public ChangeType ChangeType { get; set; }

    /// <summary>Required when <see cref="ChangeType"/> is <see cref="ChangeType.TaxRate"/>.</summary>
    [Range(0, 100)]
    public decimal? NewTaxRate { get; set; }

    /// <summary>
    /// Optional: only products currently at this rate are counted as affected.
    /// Defaults to the tenant's most common product tax rate (or AT standard 20%).
    /// </summary>
    [Range(0, 100)]
    public decimal? CurrentTaxRate { get; set; }

    /// <summary>Required when <see cref="ChangeType"/> is <see cref="ChangeType.Currency"/>.</summary>
    [MaxLength(3)]
    public string? NewCurrency { get; set; }

    /// <summary>Required when <see cref="ChangeType"/> is <see cref="ChangeType.ProductPrice"/>.</summary>
    public List<ProductPriceUpdateDto>? ProductPriceUpdates { get; set; }
}

public sealed class ImpactAffectedRecordsDto
{
    public int Products { get; set; }
    public int Payments { get; set; }
    public int Invoices { get; set; }
}

public sealed class ImpactReportDto
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public ChangeType ChangeType { get; set; }
    public Guid TenantId { get; set; }
    public ImpactAffectedRecordsDto AffectedRecords { get; set; } = new();
    public decimal? EstimatedFinancialImpact { get; set; }
    public string? EstimatedFinancialImpactCurrency { get; set; }
    public IReadOnlyList<string> Recommendations { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
    public string Severity { get; set; } = ImpactSeverity.Info;
}

public static class ImpactSeverity
{
    public const string Info = "Info";
    public const string Warning = "Warning";
    public const string Critical = "Critical";
}

/// <summary>Domain report returned by the simulation service (maps 1:1 to <see cref="ImpactReportDto"/>).</summary>
public sealed class ImpactReport
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public ChangeType ChangeType { get; set; }
    public Guid TenantId { get; set; }
    public ImpactAffectedRecordsDto AffectedRecords { get; set; } = new();
    public decimal? EstimatedFinancialImpact { get; set; }
    public string? EstimatedFinancialImpactCurrency { get; set; }
    public IReadOnlyList<string> Recommendations { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
    public string Severity { get; set; } = ImpactSeverity.Info;

    public ImpactReportDto ToDto() =>
        new()
        {
            Title = Title,
            Summary = Summary,
            ChangeType = ChangeType,
            TenantId = TenantId,
            AffectedRecords = AffectedRecords,
            EstimatedFinancialImpact = EstimatedFinancialImpact,
            EstimatedFinancialImpactCurrency = EstimatedFinancialImpactCurrency,
            Recommendations = Recommendations,
            Warnings = Warnings,
            Severity = Severity,
        };
}

/// <summary>In-memory product price update payload for simulation (not an EF entity).</summary>
public sealed class ProductPriceUpdate
{
    public Guid ProductId { get; set; }
    public decimal NewPrice { get; set; }
}
